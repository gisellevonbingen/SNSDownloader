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
using SNSDownloader.Configs;
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
        public static TwitterTweetDownloader TwitterTweetDownloader { get; private set; }
        public static TwitterTimelineSearchDownloader TwitterTimelineSearchDownloader { get; private set; }
        public static TwitterTimelineUserDownloader TwitterTimelineUserDownloader { get; private set; }
        public static TwitterAudioSpaceDownloader TwitterSpaceDownloader { get; private set; }
        public static TiktokDownloader TiktokDownloader { get; private set; }
        private static IWebDriver Driver;

        private static ChromeOptions TwitterLoginOptions;
        private static ChromeOptions CrawlOptions;

        private static int ReaminCrawlCount = 0;
        private static bool TwitterLogined = false;
        private static bool FFmpegEntered = false;

        public static string ConfigPath { get; } = Path.Combine(Directory.GetCurrentDirectory(), "Config.json");
        public static Config Config { get; private set; }

        public static void Main()
        {
            if (!ReloadConfig())
            {
                return;
            }

            TwitterLoginOptions = new ChromeOptions();

            CrawlOptions = new ChromeOptions();
            CrawlOptions.AddArgument("--headless --mute-audio");

            try
            {
                Downloaders.Add(TwitterTweetDownloader = new TwitterTweetDownloader());
                Downloaders.Add(TwitterTimelineSearchDownloader = new TwitterTimelineSearchDownloader());
                Downloaders.Add(TwitterTimelineUserDownloader = new TwitterTimelineUserDownloader());
                Downloaders.Add(TwitterSpaceDownloader = new TwitterAudioSpaceDownloader());
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
                    Config = new Config(json);
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

                try
                {
                    var urls = JArray.Parse(File.ReadAllText(input)).Select(v => v.Value<string>()).ToList();

                    for (var ui = 0; ui < urls.Count; ui++)
                    {
                        var url = urls[ui];
                        var skip = progressed.Contains(ProcessProgressUrl(url));

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
                catch (Exception e)
                {
                    Console.WriteLine($"Exception: {input}, {e}");
                }

            }

            if (skippedCount > 0)
            {
                Console.WriteLine($"Skipped Count: {skippedCount}");
                skippedCount = 0;
            }

        }

        public static bool Operate<DOWNLOADER>(DOWNLOADER downloader, string url, Func<DOWNLOADER, bool> func) where DOWNLOADER : AbstractDownloader
        {
            downloader.Reset();

            if (!downloader.Ready(url))
            {
                return false;
            }

            NavigateToDownload(downloader);
            return func(downloader);
        }

        private static void Download(ProgressTracker progressed, string outputDirectory, string url)
        {
            Console.WriteLine($"=> {url}");

            Downloaders.ForEach(d => d.Reset());
            var downloader = Downloaders.FirstOrDefault(d => d.Ready(url));

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
                    downloader.Log($"Ready");

                    NavigateToDownload(downloader);

                    if (downloader.Download(output))
                    {
                        foreach (var child in downloader.Children.ToList())
                        {
                            if (!progressed.Contains(ProcessProgressUrl(child)))
                            {
                                Download(progressed, outputDirectory, child);
                            }

                        }

                        if (downloader.CanSkip)
                        {
                            progressed.Add(ProcessProgressUrl(url));
                        }

                        break;
                    }

                }
                catch (Exception e)
                {
                    downloader.Log($"Exception: {url}, {e}");
                }
                finally
                {
                    Thread.Sleep(5000);
                }

            }

        }

        private static string ProcessProgressUrl(string url) => url.Replace("twitter.com", "x.com");

        private static void NavigateToDownload(AbstractDownloader downloader)
        {
            if ((downloader == TwitterTweetDownloader || downloader == TwitterTimelineSearchDownloader || downloader == TwitterSpaceDownloader) && !TwitterLogined)
            {
                if (Config.Twitter.Cookies.Count == 0)
                {
                    RecreateDriver(TwitterLoginOptions);
                    Driver.Navigate().GoToUrl("https://x.com/i/flow/login/");

                    Console.WriteLine("First, login twitter account");
                    Console.Write("Press enter to start after login");
                    Console.ReadLine();

                    ReaminCrawlCount = 0;
                    Config.Twitter.Cookies.AddRange(GetCookies(Driver, "https://x.com/"));
                    SaveConfig();
                }

                TwitterLogined = true;
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
                var cookieUrls = new string[] { };

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

            PutCookies(driver, "https://x.com/", Config.Twitter.Cookies);

            return driver;
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
            using var responseStream = response.ReadAsStream();
            DownloadBlob(path, responseStream);
        }

        public static void DownloadBlob(string path, Stream responseStream)
        {
            using var mediaStream = new FileStream(path, FileMode.Create);
            responseStream.CopyTo(mediaStream, DownloadBufferSize);
        }

        public static void DownloadBlob(string directory, string fileNamePrefix, HttpWebResponse response) => DownloadBlob(GetMediaFilePath(directory, fileNamePrefix, response.ResponseUri), response);

        public static void DownloadBlob(string directory, string fileNamePrefix, string fileName, Stream response) => DownloadBlob(GetMediaFilePath(directory, fileNamePrefix, fileName), response);

        public static string GetMediaFilePath(string directory, string prefix, string name) => Path.Combine(directory, $"{prefix}_{name}"); 

        public static string GetMediaFilePath(string directory, string prefix, Uri uri) => GetMediaFilePath(directory, prefix, Path.GetFileName(uri.LocalPath));

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
                using var process = Process.Start(new ProcessStartInfo(Config.FFmpegPath, $"-i \"{downloadData.Url}\" -c copy -y \"{path}\""));
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
            PutHeaders(request, e.RequestHeaders);
        }

        public static void PutHeaders(HttpWebRequest request, IEnumerable<KeyValuePair<string, string>> headers)
        {
            foreach (var pair in headers)
            {
                request.Headers[pair.Key] = pair.Value;
            }

        }

    }

}
