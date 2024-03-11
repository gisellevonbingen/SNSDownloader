using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SNSDownloader.Net;
using SNSDownloader.Tiktok;
using SNSDownloader.Twitter;
using SNSDownloader.Util;

namespace SNSDownloader
{
    public class Program
    {
        public static int DownloadBufferSize { get; } = 16 * 1024;
        public static Encoding UTF8WithoutBOM = new UTF8Encoding(false);

        private static readonly List<AbstractDownloader> Downloaders = new List<AbstractDownloader>();
        private static TwitterTweetDownloader TwitterTweetDownloader;
        private static TwitterTimelineDownloader TwitterTimelineDownloader;
        private static TiktokDownloader TiktokDownloader;
        private static IWebDriver Driver;

        private static ChromeOptions TwitterLoginOptions;
        private static ChromeOptions CrawlOptions;

        private static int ReaminCrawlCount = 0;
        private static bool TwitterLogined = false;
        private static bool FFmpegEntered = false;

        public static string ConfigPath { get; } = Path.Combine(Directory.GetCurrentDirectory(), "Config.json");
        public static Config Config { get; } = new Config();

        public static void Main()
        {
            if (!ReloadConfig())
            {
                return;
            }

            TwitterLoginOptions = new ChromeOptions();

            CrawlOptions = new ChromeOptions();
            CrawlOptions.AddArgument("--headless");

            try
            {
                Downloaders.Add(TwitterTweetDownloader = new TwitterTweetDownloader());
                Downloaders.Add(TwitterTimelineDownloader = new TwitterTimelineDownloader());
                Downloaders.Add(TiktokDownloader = new TiktokDownloader());

                Console.CancelKeyPress += (sender, e) => Driver.DisposeQuietly();
                AppDomain.CurrentDomain.ProcessExit += (sender, e) => Driver.DisposeQuietly();

                Run();
            }
            finally
            {
                Downloaders.ForEach(d => d.DisposeQuietly());
                Driver.DisposeQuietly();
            }

        }

        public static bool ReloadConfig()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    var json = JObject.Parse(File.ReadAllText(ConfigPath));
                    Config.Load(json);
                    SaveConfig();
                    return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return false;
                }

            }
            else
            {
                SaveConfig();
                return true;
            }

        }

        private static void SaveConfig()
        {
            var json = new JObject();
            Config.Save(json);
            File.WriteAllText(ConfigPath, json.ToString());
        }

        private static void Run()
        {
            var inputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Input");
            Directory.CreateDirectory(inputDirectory);
            var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Output");
            Directory.CreateDirectory(outputDirectory);

            var inputs = Directory.GetFiles(inputDirectory, "*.json", SearchOption.AllDirectories);
            var progressed = new ProgressTracker(Path.Combine(inputDirectory, "progress.txt"));

            Console.WriteLine($"Found input files: {inputs.Length}");
            Console.WriteLine();

            var skippedCount = 0;

            for (var di = 0; di < inputs.Length; di++)
            {
                var input = inputs[di];
                var urls = JArray.Parse(File.ReadAllText(input)).Select(v => v.Value<string>()).ToList();

                for (var ui = 0; ui < urls.Count; ui++)
                {
                    var url = urls[ui];
                    var skip = progressed.Contains(url);

                    if (!skip || Config.LogSkipped)
                    {
                        if (skippedCount > 0)
                        {
                            Console.WriteLine($"Skipped Count: {skippedCount}");
                            Console.WriteLine();
                            skippedCount = 0;
                        }

                        Console.WriteLine($"Input: {di + 1} / {inputs.Length}, {(di + 1) / (inputs.Length / 100.0F):F2}%");
                        Console.WriteLine($"=> {input}");
                        Console.WriteLine($"Url: {ui + 1} / {urls.Count}, {(ui + 1) / (urls.Count / 100.0F):F2}%");
                        Console.WriteLine($"=> {url}");
                    }
                    else
                    {
                        skippedCount++;
                    }

                    if (skip)
                    {
                        if (Config.LogSkipped)
                        {
                            Console.WriteLine("Skipped");
                            Console.WriteLine();
                        }

                        continue;
                    }

                    Download(progressed, outputDirectory, url);
                    Console.WriteLine();
                }

            }

            if (skippedCount > 0)
            {
                Console.WriteLine($"Skipped Count: {skippedCount}");
                skippedCount = 0;
            }

        }

        private static void Download(ProgressTracker progressed, string outputDirectory, string url)
        {
            Downloaders.ForEach(d => d.Reset());
            var downloader = Downloaders.FirstOrDefault(d => d.Test(url));

            if (downloader == null)
            {
                Console.WriteLine("Downloader not found");
                return;
            }

            var output = new DownloadOutput(progressed, Path.Combine(outputDirectory, downloader.PlatformName));
            Directory.CreateDirectory(output.Directory);

            while (true)
            {
                try
                {
                    var ready = downloader.Ready(url);
                    downloader.Log($"Ready: {ready}");

                    if (ready == true)
                    {
                        NavigateToDownload(downloader);

                        if (downloader.Download(output))
                        {
                            progressed.Add(url);
                            break;
                        }

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

        private static void NavigateToDownload(AbstractDownloader downloader)
        {
            if ((downloader == TwitterTweetDownloader || downloader == TwitterTimelineDownloader) && !TwitterLogined)
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
            Driver.Navigate().GoToUrl(downloader.GetRequestUrl());
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

        public static void DownloadLargest(string directory, string fileNamePrefix, IEnumerable<MediaDownloadData> downloads)
        {
            foreach (var download in downloads.OrderByDescending(o => o.Size.Width * o.Size.Height))
            {
                var path = Path.Combine(directory, $"{fileNamePrefix}_{download.Size.Width}x{download.Size.Height}_{Path.GetFileName(new Uri(download.Url).LocalPath)}");
                DownloadMedia(path, download);

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

        public static void DownloadBlob(string path, HttpWebResponse response)
        {
            using var mediaStream = new FileStream(path, FileMode.Create);
            DownloadBlob(mediaStream, response);
        }

        public static void DownloadBlob(Stream output, HttpWebResponse response)
        {
            using var responseStream = response.ReadAsStream();
            responseStream.CopyTo(output, DownloadBufferSize);
        }

        public static void DownloadBlob(string directory, string fileNamePrefix, HttpWebResponse response) => DownloadBlob(Path.Combine(directory, $"{fileNamePrefix}_{Path.GetFileName(response.ResponseUri.LocalPath)}"), response);

        public static void DownloadMedia(string path, MediaDownloadData downloadData)
        {
            if (downloadData.Type == MediaDownloadData.DownloadType.Blob)
            {
                using var response = CreateRequest(downloadData.Url).GetWrappedResponse();
                DownloadBlob(path, response.Response);
            }
            else if (downloadData.Type == MediaDownloadData.DownloadType.M3U)
            {
                while (!FFmpegEntered && (string.IsNullOrEmpty(Config.FFmpegPath) || !File.Exists(Config.FFmpegPath)))
                {
                    Console.WriteLine($"Need FFmpeg for download media: {downloadData.Url}");
                    Console.WriteLine("Enter FFmpeg.exe path");
                    Console.WriteLine("Empty to skip");
                    Console.Write(">");
                    Config.FFmpegPath = Console.ReadLine();

                    if (string.IsNullOrEmpty(Config.FFmpegPath) || File.Exists(Config.FFmpegPath))
                    {
                        FFmpegEntered = true;
                        SaveConfig();
                        break;
                    }

                }

                path = Path.ChangeExtension(path, ".mp4");
                using var process = Process.Start(new ProcessStartInfo(Config.FFmpegPath, $"-i \"{downloadData.Url}\" -c copy \"{path}\""));
                process.WaitForExit();
            }

        }

        public static HttpWebRequest CreateRequest(string url) => WebRequest.CreateHttp(url);

        public static HttpWebRequest CreateRequest(Uri uri) => WebRequest.CreateHttp(uri);

        public static HttpWebRequest CreateRequest(NetworkRequestSentEventArgs e)
        {
            var request = CreateRequest(e.RequestUrl);
            Bind(request, e);

            return request;
        }

        public static void Bind(HttpWebRequest request, NetworkRequestSentEventArgs e)
        {
            request.Method = e.RequestMethod;

            foreach (var pair in e.RequestHeaders)
            {
                request.Headers[pair.Key] = pair.Value;
            }
        }

    }

}
