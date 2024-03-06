using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SNSDownloader.Tiktok;
using SNSDownloader.Twitter;

namespace SNSDownloader
{
    public class Program
    {
        public static string MapUriPrefix { get; } = "#EXT-X-MAP:URI";
        public static int DownloadBufferSize { get; } = 16 * 1024;
        public static Encoding UTF8WithoutBOM = new UTF8Encoding(false);

        private static readonly List<AbstractDownloader> Downloaders = new List<AbstractDownloader>();
        private static TwitterDownloader TwitterDownloader;
        private static TiktokDownloader TiktokDownloader;
        private static IWebDriver Driver;

        private static ChromeOptions TwitterLoginOptions;
        private static ChromeOptions CrawlOptions;

        private static int ReaminCrawlCount = 0;
        private static bool TwitterLogined = false;

        public static void Main()
        {
            TwitterLoginOptions = new ChromeOptions();

            CrawlOptions = new ChromeOptions();
            CrawlOptions.AddArgument("--headless");

            try
            {
                Downloaders.Add(TwitterDownloader = new TwitterDownloader());
                Downloaders.Add(TiktokDownloader = new TiktokDownloader());

                Console.CancelKeyPress += (sender, e) => Driver.DisposeQuietly();

                Run();
            }
            finally
            {
                Downloaders.ForEach(d => d.DisposeQuietly());
                Driver.DisposeQuietly();
            }

        }

        private static void Run()
        {
            var inputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Input");
            Directory.CreateDirectory(inputDirectory);
            var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Output");
            Directory.CreateDirectory(outputDirectory);

            var inputs = Directory.GetFiles(inputDirectory, "*.json");
            var progressed = new ProgressTracker(Path.Combine(inputDirectory, "progress.txt"));

            Console.WriteLine($"Found input files: {inputs.Length}");
            Console.WriteLine();

            for (var di = 0; di < inputs.Length; di++)
            {
                var input = inputs[di];
                var urls = JArray.Parse(File.ReadAllText(input)).Select(v => v.Value<string>()).ToList();

                for (var ui = 0; ui < urls.Count; ui++)
                {
                    var url = urls[ui];

                    Console.WriteLine($"Input: {di + 1} / {inputs.Length}, {(di + 1) / (inputs.Length / 100.0F):F2}%");
                    Console.WriteLine($"=> {input}");
                    Console.WriteLine($"Url: {ui + 1} / {urls.Count}, {(ui + 1) / (urls.Count / 100.0F):F2}%");
                    Console.WriteLine($"=> {url}");

                    Download(progressed, outputDirectory, url);

                    Console.WriteLine();
                }

            }

        }

        private static void Download(ProgressTracker progressed, string outputDirectory, string url)
        {
            if (progressed.Contains(url) == true)
            {
                Console.WriteLine("Skipped");
                return;
            }

            Downloaders.ForEach(d => d.Reset());
            var downloader = Downloaders.FirstOrDefault(d => d.Test(url));

            if (downloader == null)
            {
                Console.WriteLine("Downloader not found");
                return;
            }

            while (true)
            {
                try
                {
                    downloader.Log("Fetching");
                    Crawl(downloader, url);

                    if (downloader.Download(url, Directory.CreateDirectory(Path.Combine(outputDirectory, downloader.PlatformName)).FullName))
                    {
                        progressed.Add(url);
                        break;
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    Thread.Sleep(5000);
                }

            }

        }

        private static void Crawl(IDownloader downloader, string url)
        {
            if (downloader == TwitterDownloader && !TwitterLogined)
            {
                RecreateDriver(TwitterLoginOptions);
                Driver.Navigate().GoToUrl("https://twitter.com/i/flow/login/");

                Console.WriteLine("First, login twitter account");
                Console.Write("Press enter to start after login");
                Console.ReadLine();

                TwitterLogined = true;
                ReaminCrawlCount = 0;
            }

            if (ReaminCrawlCount <= 0)
            {
                ReaminCrawlCount = 50;
                RecreateDriver(CrawlOptions);

                var network = Driver.Manage().Network;
                Downloaders.ForEach(d => d.OnNetworkCreated(network));

                network.StartMonitoring().Wait();
            }

            ReaminCrawlCount--;
            Driver.Navigate().GoToUrl(url);
        }

        private static void RecreateDriver(ChromeOptions options)
        {
            var prev = Driver;
            var next = RecreateDriver(prev, options);
            Driver = next;
        }

        private static IWebDriver RecreateDriver(IWebDriver prev, ChromeOptions options)
        {
            var cookieMap = new Dictionary<string, OpenQA.Selenium.Cookie[]>();

            if (prev != null)
            {
                var cookieUrls = new string[] { "https://twitter.com/" };

                foreach (var url in cookieUrls)
                {
                    cookieMap[url] = GetCookies(prev, url);
                }

                prev.DisposeQuietly();
            }

            var driverService = ChromeDriverService.CreateDefaultService(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            driverService.HideCommandPromptWindow = true;
            driverService.SuppressInitialDiagnosticInformation = true;
            var driver = new ChromeDriver(driverService, options);

            foreach (var (url, cookies) in cookieMap)
            {
                PutCookies(driver, url, cookies);
            }

            return driver;
        }

        public static void DownloadLargest(string fileNamePrefix, string directory, IEnumerable<DownloadData> downloads)
        {
            foreach (var download in downloads.OrderByDescending(o => o.Size.Width * o.Size.Height))
            {
                if (download.Segments.Count == 0)
                {
                    continue;
                }

                var fileName = $"{fileNamePrefix}_{download.Size.Width}x{download.Size.Height}_{Path.GetFileName(download.Segments[0].LocalPath)}";

                using var mediaStream = new FileStream(Path.Combine(directory, fileName), FileMode.Create);
                DownloadVideo(mediaStream, download.Segments);

                break;
            }

        }

        private static OpenQA.Selenium.Cookie[] GetCookies(IWebDriver driver, string url)
        {
            driver.Navigate().GoToUrl(url);
            return driver.Manage().Cookies.AllCookies.ToArray();
        }

        private static void PutCookies(IWebDriver driver, string url, IEnumerable<OpenQA.Selenium.Cookie> cookies)
        {
            driver.Navigate().GoToUrl(url);
            driver.Manage().Cookies.DeleteAllCookies();

            foreach (var cookie in cookies)
            {
                driver.Manage().Cookies.AddCookie(cookie);
            }

        }

        public static void DownloadSimpleMedia(string path, HttpWebResponse response)
        {
            using var responseStream = response.ReadAsStream();
            using var mediaStream = new FileStream(path, FileMode.Create);
            responseStream.CopyTo(mediaStream, DownloadBufferSize);
        }

        public static void DownloadSimpleMedia(string directory, string mediaFilePrefix, HttpWebResponse response) => DownloadSimpleMedia(Path.Combine(directory, $"{mediaFilePrefix}_{Path.GetFileName(response.ResponseUri.LocalPath)}"), response);

        public static void DownloadVideo(Stream output, IEnumerable<Uri> segments)
        {
            foreach (var segment in segments)
            {
                using var segmentResponse = GetResponse(segment);
                using var segmentResponseStream = segmentResponse.Response.ReadAsStream();
                segmentResponseStream.CopyTo(output, DownloadBufferSize);
            }

        }

        public static IEnumerable<string> GetM3U8Segments(HttpWebResponse response)
        {
            using var responseReader = response.ReadAsReader(UTF8WithoutBOM);

            for (string line = null; (line = responseReader.ReadLine()) != null;)
            {
                if (line.StartsWith(MapUriPrefix) == true)
                {
                    //#EXT-X-MAP:URI=
                    var mapUri = line[(MapUriPrefix.Length + 1)..];

                    if (mapUri.StartsWith("\"") == true && mapUri.EndsWith("\"") == true)
                    {
                        yield return mapUri[1..^1];
                    }
                    else
                    {
                        yield return mapUri;
                    }

                }
                else if (line.StartsWith("#") == true)
                {
                    continue;
                }
                else
                {
                    yield return line;
                }

            }

        }

        public static WrappedResponse GetResponse(string url) => GetResponse(WebRequest.CreateHttp(url));

        public static WrappedResponse GetResponse(Uri uri) => GetResponse(WebRequest.CreateHttp(uri));

        public static WrappedResponse GetResponse(NetworkRequestSentEventArgs e)
        {
            var request = WebRequest.CreateHttp(e.RequestUrl);
            request.Method = e.RequestMethod;

            foreach (var pair in e.RequestHeaders)
            {
                request.Headers[pair.Key] = pair.Value;
            }

            return GetResponse(request);
        }

        public static WrappedResponse GetResponse(HttpWebRequest request)
        {
            try
            {
                return new WrappedResponse(request, request.GetResponse() as HttpWebResponse, true);
            }
            catch (WebException e)
            {
                return new WrappedResponse(request, e.Response as HttpWebResponse, false);
            }

        }

    }

}
