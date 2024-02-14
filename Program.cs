using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace TwitterVideoDownloader
{
    public class Program
    {
        public static string TweetIdGroup { get; } = "tweet_id";
        public static Regex XStatusPattern { get; } = new Regex($"https:\\/\\/x\\.com\\/(?<user_id>.+)\\/status\\/(?<{TweetIdGroup}>\\d+)(\\?.+)?");
        public static Regex TweetStatusPattern { get; } = new Regex($"https:\\/\\/twitter\\.com\\/(?<user_id>.+)\\/status\\/(?<{TweetIdGroup}>\\d+)(\\?.+)?");
        public static Regex TweetDetailPattern { get; } = new Regex("https:\\/\\/twitter\\.com\\/i\\/api\\/graphql\\/.+\\/TweetDetail");
        public static Regex SizePattern { get; } = new Regex("(?<width>\\d+)x(?<height>\\d+)");
        public static Regex SrcPattern { get; } = new Regex("src=\"(?<src>[^\"]+)\"");
        public static string MapUriPrefix { get; } = "#EXT-X-MAP:URI";
        public static int DownloadBufferSize { get; } = 16 * 1024;
        public static Encoding UTF8WithoutBOM = new UTF8Encoding(false);

        private static IWebDriver Driver;

        public static void Main()
        {
            var loginOptions = new ChromeOptions();
            //loginOptions.AddArgument("--headless");

            var crawlOptions = new ChromeOptions();
            crawlOptions.AddArgument("--headless");

            Console.WriteLine("Driver starting");
            RecreateDriver(loginOptions);
            Driver.Navigate().GoToUrl("https://twitter.com/");

            Console.CancelKeyPress += (sender, e) =>
            {
                Driver.Dispose();
            };

            Console.WriteLine("First, login twitter account");
            Console.Write("Press enter to start after login");
            Console.ReadLine();

            var reaminCrawlCount = 0;

            using var are = new TweetFetcher();
            are.Requested += (sender, url) =>
            {
                if (reaminCrawlCount <= 0)
                {
                    reaminCrawlCount = 50;
                    RecreateDriver(crawlOptions);
                    Driver.Manage().Network.NetworkResponseReceived += (sender, e) => OnNetworkResponseReceived(e, are);
                    Driver.Manage().Network.StartMonitoring().Wait();
                }

                reaminCrawlCount--;
                Driver.Navigate().GoToUrl(url);
            };

            var directories = Directory.GetDirectories(Directory.GetCurrentDirectory());

            for (var di = 0; di < directories.Length; di++)
            {
                var directory = directories[di];
                var inputFile = Path.Combine(directory, "tweets.json");

                if (File.Exists(inputFile) == false)
                {
                    continue;
                }

                var urls = JArray.Parse(File.ReadAllText(inputFile)).Select(v => v.Value<string>()).ToList();

                var progressFile = Path.Combine(directory, "progress.txt");
                var progressed = new HashSet<string>();

                if (File.Exists(progressFile) == true)
                {
                    foreach (var line in File.ReadAllLines(progressFile))
                    {
                        progressed.Add(line);
                    }

                }

                for (int ui = 0; ui < urls.Count;)
                {
                    var url = urls[ui];
                    var tweetId = GetTweetId(url);

                    if (string.IsNullOrWhiteSpace(tweetId) == true)
                    {
                        Console.WriteLine("Wrong tweet url");
                        ui++;
                    }
                    else
                    {
                        Console.WriteLine($"Directory: {di + 1} / {directories.Length}, {(di + 1) / (directories.Length / 100.0F):F2}%");
                        Console.WriteLine($"=> {directories[di]}");
                        Console.WriteLine($"Tweet: {ui + 1} / {urls.Count}, {(ui + 1) / (urls.Count / 100.0F):F2}%");
                        Console.WriteLine($"=> {url}");

                        if (progressed.Contains(tweetId) == true)
                        {
                            Console.WriteLine("Tweet skipped");
                            ui++;
                        }
                        else
                        {
                            try
                            {
                                Console.WriteLine("Tweet fetching");
                                var tweets = are.Fetch(url).ToList();

                                if (tweets.Count == 0)
                                {
                                    Console.WriteLine("Tweet not found");
                                }
                                else
                                {
                                    Console.WriteLine($"Tweet found : {tweets.Count}");
                                    DownloadTweet(tweets, tweetId, directory);
                                    ui++;

                                    progressed.Add(tweetId);
                                    File.AppendAllLines(progressFile, new[] { tweetId });
                                }

                            }
                            finally
                            {
                                Thread.Sleep(5000);
                            }

                        }

                        Console.WriteLine();
                    }

                }

            }

        }

        private static void RecreateDriver(ChromeOptions options)
        {
            var prev = Driver;
            var next = RecreateDriver(prev, options);
            Driver = next;
        }

        private static IWebDriver RecreateDriver(IWebDriver prev, ChromeOptions options)
        {
            var cookies = new List<OpenQA.Selenium.Cookie>();

            if (prev != null)
            {
                cookies.AddRange(GetCookies(prev));
                prev.Dispose();
            }

            var driverService = ChromeDriverService.CreateDefaultService(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            driverService.HideCommandPromptWindow = true;
            driverService.SuppressInitialDiagnosticInformation = true;
            var driver = new ChromeDriver(driverService, options);

            if (cookies.Count > 0)
            {
                PutCookies(driver, cookies);
            }

            return driver;
        }

        private static OpenQA.Selenium.Cookie[] GetCookies(IWebDriver driver)
        {
            driver.Navigate().GoToUrl("https://twitter.com/");
            return driver.Manage().Cookies.AllCookies.ToArray();
        }

        private static void PutCookies(IWebDriver driver, IEnumerable<OpenQA.Selenium.Cookie> cookies)
        {
            driver.Navigate().GoToUrl("https://twitter.com/");

            foreach (var cookie in cookies)
            {
                driver.Manage().Cookies.AddCookie(cookie);
            }

        }

        private static string GetTweetId(string url)
        {
            var statusMatch = TweetStatusPattern.Match(url);

            if (statusMatch.Success == false)
            {
                statusMatch = XStatusPattern.Match(url);

                if (statusMatch.Success == false)
                {
                    return null;
                }

            }

            return statusMatch.Groups[TweetIdGroup].Value;
        }

        private static void DownloadTweet(List<TwitterTwitInfo> tweetList, string tweetId, string directory)
        {
            var found = tweetList.FirstOrDefault(i => i.Id.Equals(tweetId));
            var tweetPrefix = $"{found.CreatedAt.ToLocalTime():yyyyMMdd_HHmmss}_{found.User.ScreenName}_{tweetId}";
            var downladIndex = 0;
            using var tweetStream = new FileStream(Path.Combine(directory, $"{tweetPrefix}.txt"), FileMode.Create);
            using var tweetWriter = new StreamWriter(tweetStream, UTF8WithoutBOM);
            var mediaUrls = new HashSet<string>();

            for (var ti = 0; ti < tweetList.Count; ti++)
            {
                var tweet = tweetList[ti];
                Console.WriteLine($"Media found : {tweet.Media.Count}");

                {
                    if (ti > 0)
                    {
                        tweetWriter.WriteLine();
                        tweetWriter.WriteLine(new string('=', 40));
                        tweetWriter.WriteLine();
                    }

                    WriteTweet(tweetWriter, tweet);
                }

                foreach (var media in tweet.Media)
                {
                    if (mediaUrls.Contains(media.Url) == true)
                    {
                        continue;
                    }

                    var mediaFilePrefix = $"{tweetPrefix}_{++downladIndex}";
                    DownloadMedia(directory, mediaFilePrefix, media);
                    mediaUrls.Add(media.Url);
                }

            }

        }

        private static void WriteTweet(TextWriter tweetWriter, TwitterTwitInfo tweet)
        {
            tweetWriter.WriteLine($"CreatedAt: {tweet.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            tweetWriter.WriteLine($"Url: https://twitter.com/{tweet.User.ScreenName}/status/{tweet.Id}");
            tweetWriter.WriteLine($"User: {tweet.User.Name}(@{tweet.User.ScreenName})");
            tweetWriter.WriteLine($"Id: {tweet.Id}");
            tweetWriter.WriteLine($"Quoted: {tweet.Quoted}");
            tweetWriter.WriteLine($"Media: {tweet.Media.Count}");

            for (var mi = 0; mi < tweet.Media.Count; mi++)
            {
                tweetWriter.WriteLine($"- {tweet.Media[mi].Url}");
            }

            tweetWriter.WriteLine();
            tweetWriter.WriteLine(tweet.FullText);
        }

        private static void DownloadMedia(string directory, string mediaFilePrefix, TwitterMediaEntity media)
        {
            if (media is TwitterMediaPhotoEntity photo)
            {
                DownloadSimpleMedia(directory, mediaFilePrefix, photo.RequestUrl);
            }
            else if (media is TwitterMediaTwitPicEntity twitpic)
            {
                using var pageResponse = WebRequest.CreateHttp(twitpic.Url).GetResponse();
                using var pageStream = pageResponse.GetDecompressedResponseStream();

                var html = UTF8WithoutBOM.GetString(pageStream.ToArray());
                var groups = SrcPattern.Match(html).Groups;
                var src = groups["src"].Value;

                DownloadSimpleMedia(directory, mediaFilePrefix, src);
            }
            else if (media is TwitterMediaVideoEntity video)
            {
                var variants = video.VideoInfo.Variants;
                var downloadList = variants.SelectMany(v => GetDownloadDataList(video, v)).ToArray();

                foreach (var download in downloadList.OrderByDescending(o => o.Size.Width * o.Size.Height))
                {
                    if (download.Segments.Count == 0)
                    {
                        continue;
                    }

                    var fileName = $"{mediaFilePrefix}_{download.Size.Width}x{download.Size.Height}_{Path.GetFileName(download.Segments[0].LocalPath)}";

                    using var mediaStream = new FileStream(Path.Combine(directory, fileName), FileMode.Create);
                    DownloadVideo(mediaStream, download.Segments);

                    break;
                }

            }

        }

        private static void DownloadSimpleMedia(string directory, string mediaFilePrefix, string url)
        {
            try
            {
                using var mediaStream = new FileStream(Path.Combine(directory, $"{mediaFilePrefix}_{Path.GetFileName(new Uri(url).LocalPath)}"), FileMode.Create);
                using var response = WebRequest.CreateHttp(url).GetResponse();
                using var responseStream = response.GetResponseStream();
                responseStream.CopyTo(mediaStream, DownloadBufferSize);
            }
            catch (WebException e)
            {
                Console.WriteLine(e);
            }

        }

        private static void DownloadVideo(Stream output, IEnumerable<Uri> segments)
        {
            try
            {
                foreach (var segment in segments)
                {
                    using var segmentResponse = WebRequest.CreateHttp(segment).GetResponse();
                    using var segmentResponseStream = segmentResponse.GetResponseStream();
                    segmentResponseStream.CopyTo(output, DownloadBufferSize);
                }

            }
            catch (WebException e)
            {
                Console.WriteLine(e);
            }

        }

        private static IEnumerable<DownloadData> GetDownloadDataList(TwitterMediaVideoEntity video, TwitterVideoVariant variant)
        {
            if (variant.ContentType.Equals("video/mp4") == true)
            {
                var size = ParseSize(variant.Url);
                yield return new DownloadData(new Uri(variant.Url)) { Size = size };
            }
            else if (variant.ContentType.Equals("application/x-mpegURL") == true)
            {
                var variantUri = new Uri(variant.Url);
                using var response = WebRequest.CreateHttp(variantUri).GetResponse();
                using var responseReader = new StreamReader(response.GetResponseStream());

                for (string line = null; (line = responseReader.ReadLine()) != null;)
                {
                    if (line.StartsWith("#") == true)
                    {
                        continue;
                    }

                    var size = video.OriginalSize;

                    try
                    {
                        size = ParseSize(line);
                    }
                    catch (Exception)
                    {

                    }

                    var m3u8Uri = new Uri(variantUri, line);
                    var segments = GetM3U8Segments(m3u8Uri).Select(s => new Uri(m3u8Uri, s));
                    yield return new DownloadData(segments) { Size = size };
                }

            }
            else
            {
                Console.WriteLine($"Unknown ContentType: {variant.ContentType}");
            }

        }

        private static IEnumerable<string> GetM3U8Segments(Uri uri)
        {
            using var response = WebRequest.CreateHttp(uri).GetResponse();
            using var responseReader = new StreamReader(response.GetResponseStream());

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

        private static Size ParseSize(string url)
        {
            var groups = SizePattern.Match(url).Groups;
            var width = int.Parse(groups["width"].Value);
            var height = int.Parse(groups["height"].Value);
            return new Size(width, height);
        }

        private static void OnNetworkResponseReceived(NetworkResponseReceivedEventArgs e, TweetFetcher are)
        {
            if (TweetDetailPattern.IsMatch(e.ResponseUrl) == false || string.IsNullOrEmpty(e.ResponseBody) == true)
            {
                return;
            }

            var body = JObject.Parse(e.ResponseBody);
            are.Set(GetTweets(body));
        }

        private static IEnumerable<TwitterTwitInfo> GetTweets(JObject body)
        {
            var instructions = body.SelectToken("data.threaded_conversation_with_injections_v2.instructions");

            if (instructions == null)
            {
                yield break;
            }

            foreach (var instruction in instructions)
            {
                var instructionType = instruction.Value<string>("type");

                if (string.Equals(instructionType, "TimelineAddEntries") == false)
                {
                    continue;
                }

                foreach (var entry in instruction.Value<JArray>("entries"))
                {
                    if (TryParseTweetFromTimelineEntry(entry, out var tweet) == true)
                    {
                        yield return tweet;
                    }

                }

            }

        }

        private static bool TryParseTweetFromTimelineEntry(JToken entry, out TwitterTwitInfo tweet)
        {
            var content = entry.Value<JObject>("content");
            var entryType = content?.Value<string>("entryType");
            var itemContent = content?.Value<JObject>("itemContent");
            var itemType = itemContent?.Value<string>("itemType");

            if (string.Equals(entryType, "TimelineTimelineItem") == false || string.Equals(itemType, "TimelineTweet") == false)
            {
                tweet = null;
                return false;
            }

            var core = itemContent.SelectToken("tweet_results.result.legacy");

            if (core == null)
            {
                tweet = null;
                return false;
            }

            var user = itemContent.SelectToken("tweet_results.result.core.user_results.result.legacy");

            tweet = new TwitterTwitInfo()
            {
                Id = core.Value<string>("id_str"),
                User = new TwitterUser(user),
                CreatedAt = DateTime.ParseExact(core.Value<string>("created_at"), "ddd MMM dd HH:mm:ss K yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                FullText = core.Value<string>("full_text"),
            };

            var urlArray = core.SelectToken("entities.urls");

            if (urlArray != null)
            {
                foreach (var url in urlArray)
                {
                    var turl = new TwitterUrl(url);
                    tweet.Url.Add(turl);
                }

                foreach (var url in tweet.Url)
                {
                    tweet.FullText = tweet.FullText.Replace(url.Url, url.ExpandedUrl);

                    if (url.ExpandedUrl.StartsWith("http://twitpic.com", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        tweet.Media.Add(new TwitterMediaTwitPicEntity() { Url = url.ExpandedUrl });
                    }

                }

            }

            var quoted = core.SelectToken("quoted_status_permalink");

            if (quoted != null)
            {
                tweet.Quoted = quoted.Value<string>("expanded");
            }

            var mediaArray = core.SelectToken("extended_entities.media");

            if (mediaArray != null)
            {
                foreach (var media in mediaArray)
                {
                    var mediaType = media.Value<string>("type");

                    if (string.Equals(mediaType, "photo") == true)
                    {
                        tweet.Media.Add(new TwitterMediaPhotoEntity(media) { Large = true });
                    }
                    else if (string.Equals(mediaType, "video") == true)
                    {
                        tweet.Media.Add(new TwitterMediaVideoEntity(media));
                    }

                }

            }

            return true;
        }

    }

}
