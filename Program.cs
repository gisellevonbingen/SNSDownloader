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
        public static Regex TweetDetailPattern { get; } = new Regex("https:\\/\\/twitter\\.com\\/i\\/api\\/graphql\\/\\w+\\/TweetDetail");
        public static Regex SizePattern { get; } = new Regex("(?<width>\\d+)x(?<height>\\d+)");
        public static Regex SrcPattern { get; } = new Regex("src=\"(?<src>[^\"]+)\"");
        public static string MapUriPrefix { get; } = "#EXT-X-MAP:URI";
        public static string OutputDirectory { get; } = "./Output";
        public static int DownloadBufferSize { get; } = 16 * 1024;
        public static Encoding UTF8WithoutBOM = new UTF8Encoding(false);

        public static void Main()
        {
            var driverService = ChromeDriverService.CreateDefaultService(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            driverService.HideCommandPromptWindow = true;
            driverService.SuppressInitialDiagnosticInformation = true;

            var tabs = new List<IWebDriver>();
            var options = new ChromeOptions();
            //options.AddArgument("--headless");
            //options.AddArgument("--silent");
            Console.WriteLine("Driver starting");
            using var driver = new ChromeDriver(driverService, options);
            driver.Navigate().GoToUrl("https://twitter.com/");
            var main = driver.CurrentWindowHandle;

            using var are = new TweetFetcher();
            are.Requested += (sender, url) =>
            {
                lock (tabs)
                {
                    foreach (var t in tabs)
                    {
                        t.Close();
                    }

                    tabs.Clear();

                    driver.SwitchTo().Window(main);
                    var tab = driver.SwitchTo().NewWindow(WindowType.Tab);
                    tab.Manage().Network.StartMonitoring();
                    tabs.Add(tab);

                    tab.Navigate().GoToUrl(url);
                }

            };

            Console.WriteLine("First, login twitter account");
            Console.Write("Press enter to start after login");
            Console.ReadLine();

            var network = driver.Manage().Network;
            network.NetworkResponseReceived += (sender, e) => OnNetworkResponseReceived(e, are);

            var directories = Directory.GetDirectories(Directory.GetCurrentDirectory());

            for (int di = 0; di < directories.Length; di++)
            {
                var directory = directories[di];
                var inputFile = Path.Combine(directory, "tweets.json");

                if (File.Exists(inputFile) == false)
                {
                    continue;
                }

                var urls = JArray.Parse(File.ReadAllText(inputFile)).Select(v => v.Value<string>()).ToList();

                for (int ui = 0; ui < urls.Count;)
                {
                    try
                    {
                        var url = urls[ui];
                        var tweetId = GetTweetId(url);

                        if (string.IsNullOrWhiteSpace(tweetId) == true)
                        {
                            Console.WriteLine("Wrong tweet url");
                        }
                        else
                        {
                            Console.WriteLine($"Directory: {di + 1} / {directories.Length}, {(di + 1) / (directories.Length / 100.0F):F2}%");
                            Console.WriteLine($"=> {directories[di]}");
                            Console.WriteLine($"Tweet: {ui + 1} / {urls.Count}, {(ui + 1) / (urls.Count / 100.0F):F2}%");
                            Console.WriteLine($"=> {url}");

                            if (DownloadTweet(are, directory, url, tweetId) == true)
                            {
                                ui++;
                            }

                            Console.WriteLine();
                        }

                    }
                    finally
                    {
                        Thread.Sleep(5000);
                    }

                }

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

        private static bool DownloadTweet(TweetFetcher fetcher, string directory, string url, string tweetId)
        {
            var tweetList = fetcher.Fetch(url).ToList();

            if (tweetList.Count == 0)
            {
                Console.WriteLine("Tweet not found");
                return false;
            }
            else
            {
                Console.WriteLine($"Tweet found : {tweetList.Count}");
                DownloadTweet(tweetList, tweetId, directory);
                return true;
            }

        }

        private static void DownloadTweet(List<TwitterTwitInfo> tweetList, string tweetId, string directory)
        {
            var found = tweetList.FirstOrDefault(i => i.Id.Equals(tweetId));
            var tweetPrefix = $"{found.CreatedAt.ToLocalTime():yyyyMMdd_HHmmss}_{found.User.ScreenName}_{tweetId}";
            var downladIndex = 0;
            using var tweetStream = new FileStream(Path.Combine(directory, $"{tweetPrefix}.txt"), FileMode.Create);
            using var tweetWriter = new StreamWriter(tweetStream, UTF8WithoutBOM);

            for (var ti = 0; ti < tweetList.Count; ti++)
            {
                var tweet = tweetList[ti];
                Console.WriteLine($"Media Found : {tweet.Media.Count}");

                {
                    if (ti > 0)
                    {
                        tweetWriter.WriteLine();
                        tweetWriter.WriteLine(new string('=', 40));
                        tweetWriter.WriteLine();
                    }

                    tweetWriter.WriteLine($"CreatedAt: {tweet.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                    tweetWriter.WriteLine($"Url: https://twitter.com/{tweet.User.ScreenName}/status/{tweetId}");
                    tweetWriter.WriteLine($"User: {tweet.User.Name}(@{tweet.User.ScreenName})");
                    tweetWriter.WriteLine($"Id: {tweetId}");
                    tweetWriter.WriteLine($"Quoted: {tweet.Quoted}");
                    tweetWriter.WriteLine($"Media: {tweet.Media.Count}");
                    tweetWriter.WriteLine();
                    tweetWriter.WriteLine(tweet.FullText);

                }

                foreach (var media in tweet.Media)
                {
                    var mediaFilePrefix = $"{tweetPrefix}_{++downladIndex}";
                    DownloadMedia(directory, mediaFilePrefix, media);
                }

            }

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

                var html = Encoding.UTF8.GetString(pageStream.ToArray());
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
            using var mediaStream = new FileStream(Path.Combine(directory, $"{mediaFilePrefix}_{Path.GetFileName(new Uri(url).LocalPath)}"), FileMode.Create);
            using var response = WebRequest.CreateHttp(url).GetResponse();
            using var responseStream = response.GetResponseStream();
            responseStream.CopyTo(mediaStream, DownloadBufferSize);
        }

        private static void DownloadVideo(Stream output, IEnumerable<Uri> segments)
        {
            foreach (var segment in segments)
            {
                using var segmentResponse = WebRequest.CreateHttp(segment).GetResponse();
                using var segmentResponseStream = segmentResponse.GetResponseStream();
                segmentResponseStream.CopyTo(output, DownloadBufferSize);
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

            try
            {
                var body = JObject.Parse(e.ResponseBody);

                foreach (var tweet in GetTweets(body))
                {
                    are.Enqueue(tweet);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                are.Set();
            }

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
