using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;

namespace SNSDownloader.Twitter
{
    public class TwitterDownloader : AbstractDownloader
    {
        public static string TweetIdGroup { get; } = "tweet_id";
        public static Regex TiktokArticlePattern { get; } = new Regex("https:\\/\\/www\\.tiktok\\.com/(?<user_id>.+)/video/(?<article_id>.+)");
        public static Regex XStatusPattern { get; } = new Regex($"https:\\/\\/x\\.com\\/(?<user_id>.+)\\/status\\/(?<{TweetIdGroup}>\\d+)(\\?.+)?");
        public static Regex TweetStatusPattern { get; } = new Regex($"https:\\/\\/twitter\\.com\\/(?<user_id>.+)\\/status\\/(?<{TweetIdGroup}>\\d+)(\\?.+)?");
        public static IEnumerable<Regex> TweetStatusPatterns { get; } = new[] { XStatusPattern, TweetStatusPattern };
        public static Regex TweetDetailPattern { get; } = new Regex("https:\\/\\/twitter\\.com\\/i\\/api\\/graphql\\/.+\\/TweetDetail");
        public static Regex SrcPattern { get; } = new Regex("src=\"(?<src>[^\"]+)\"");
        public static Regex SizePattern { get; } = new Regex("(?<width>\\d+)x(?<height>\\d+)");

        private readonly List<TimelineEntry> Entires;
        private readonly AutoResetEvent ResetEvent;

        public TwitterDownloader()
        {
            this.Entires = new List<TimelineEntry>();
            this.ResetEvent = new AutoResetEvent(false);
        }

        public override string PlatformName => "Twitter";

        public override void OnNetworkCreated(INetwork network)
        {
            network.NetworkResponseReceived += this.OnNetowrkResponseReceived;
        }

        public override void Reset()
        {
            this.Entires.Clear();
            this.ResetEvent.Reset();
        }

        public override bool Test(string url) => this.Test(url, out _);

        public bool Test(string url, out string id)
        {
            id = this.GetTweetId(url);
            return !string.IsNullOrEmpty(id);
        }

        public override bool Download(string url, string outputDirectory)
        {
            if (!this.Test(url, out var id))
            {
                return false;
            }
            else if (!this.WaitAll(this.ResetEvent))
            {
                return false;
            }
            else if (this.Entires.Count == 0)
            {
                this.Log("Not found");
                return false;
            }
            else
            {
                this.Log($"Found : {this.Entires.Count}");
                this.DownloadTweet(id, outputDirectory);
                return true;
            }

        }

        private string GetTweetId(string url)
        {
            foreach (var pattern in TweetStatusPatterns)
            {
                var statusMatch = pattern.Match(url);

                if (statusMatch.Success == true)
                {
                    return statusMatch.Groups[TweetIdGroup].Value;
                }

            }

            return null;
        }

        private void DownloadTweet(string tweetId, string baseDirectory)
        {
            var tweets = this.Entires.Select(i => i.Content).OfType<TimelineEntryContentItem>().Select(item => item.Tweet).Where(t => t != null).ToArray();
            var found = tweets.FirstOrDefault(i => i.Id.Equals(tweetId));
            var directory = Path.Combine(baseDirectory, found.User.ScreenName, $"{found.CreatedAt.ToLocalTime():yyyy-MM}");
            Directory.CreateDirectory(directory);

            var tweetPrefix = $"{found.CreatedAt.ToLocalTime():yyyyMMdd_HHmmss}_{found.User.ScreenName}_{tweetId}";
            var downladIndex = 0;
            using var tweetStream = new FileStream(Path.Combine(directory, $"{tweetPrefix}.txt"), FileMode.Create);
            using var tweetWriter = new StreamWriter(tweetStream, Program.UTF8WithoutBOM);
            var mediaUrls = new HashSet<string>();

            for (var ti = 0; ti < tweets.Length; ti++)
            {
                var tweet = tweets[ti];
                this.Log($"Media found : {tweet.Media.Count}");

                {
                    if (ti > 0)
                    {
                        tweetWriter.WriteLine();
                        tweetWriter.WriteLine(new string('=', 40));
                        tweetWriter.WriteLine();
                    }

                    this.WriteTweet(tweetWriter, tweet);
                }

                foreach (var media in tweet.Media)
                {
                    if (mediaUrls.Contains(media.Url) == true)
                    {
                        continue;
                    }

                    var mediaFilePrefix = $"{tweetPrefix}_{++downladIndex}";
                    this.DownloadMedia(directory, mediaFilePrefix, media);
                    mediaUrls.Add(media.Url);
                }

            }

        }

        private void WriteTweet(TextWriter tweetWriter, TimelineTweet tweet)
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
            tweetWriter.WriteLine(HttpUtility.HtmlDecode(tweet.FullText));
        }

        private void DownloadMedia(string directory, string mediaFilePrefix, MediaEntity media)
        {
            if (media is MediaEntityPhoto photo)
            {
                using var response = Program.GetResponse(photo.RequestUrl);

                if (response.Success == true)
                {
                    Program.DownloadSimpleMedia(directory, mediaFilePrefix, response.Response);
                }

            }
            else if (media is MediaEntityTwitPic twitpic)
            {
                using var page = Program.GetResponse(twitpic.Url);

                if (page.Success == true)
                {
                    var html = page.Response.ReadAsString(Program.UTF8WithoutBOM);
                    var groups = SrcPattern.Match(html).Groups;
                    var src = groups["src"].Value;

                    using var response = Program.GetResponse(src);

                    if (response.Success == true)
                    {
                        Program.DownloadSimpleMedia(directory, mediaFilePrefix, response.Response);
                    }

                }

            }
            else if (media is MediaEntityVideo video)
            {
                var variants = video.VideoInfo.Variants;
                var downloadList = variants.SelectMany(v => this.GetDownloadDataList(video, v)).ToArray();
                Program.DownloadLargest(directory, mediaFilePrefix, downloadList);

            }

        }

        private IEnumerable<DownloadData> GetDownloadDataList(MediaEntityVideo video, VideoVariant variant)
        {
            if (variant.ContentType.Equals("video/mp4") == true)
            {
                var size = this.ParseSize(variant.Url);
                yield return new DownloadData(new Uri(variant.Url)) { Size = size };
            }
            else if (variant.ContentType.Equals("application/x-mpegURL") == true)
            {
                var variantUri = new Uri(variant.Url);
                using var response = Program.GetResponse(variantUri);
                using var responseReader = response.Response.ReadAsReader(Program.UTF8WithoutBOM);

                for (string line = null; (line = responseReader.ReadLine()) != null;)
                {
                    if (line.StartsWith("#") == true)
                    {
                        continue;
                    }

                    var size = video.OriginalSize;

                    try
                    {
                        size = this.ParseSize(line);
                    }
                    catch (Exception)
                    {

                    }

                    var m3u8Uri = new Uri(variantUri, line);
                    using var m3u8Response = Program.GetResponse(m3u8Uri);
                    var segments = Program.GetM3U8Segments(m3u8Response.Response).Select(s => new Uri(m3u8Uri, s));
                    yield return new DownloadData(segments) { Size = size };
                }

            }
            else
            {
                this.Log($"Unknown ContentType: {variant.ContentType}");
            }

        }

        private Size ParseSize(string url)
        {
            var groups = SizePattern.Match(url).Groups;
            var width = int.Parse(groups["width"].Value);
            var height = int.Parse(groups["height"].Value);
            return new Size(width, height);
        }

        private void OnNetowrkResponseReceived(object sender, NetworkResponseReceivedEventArgs e)
        {
            if (TweetDetailPattern.IsMatch(e.ResponseUrl) == false || string.IsNullOrEmpty(e.ResponseBody) == true)
            {
                return;
            }

            var body = JObject.Parse(e.ResponseBody);
            var instructions = body.SelectToken("data.threaded_conversation_with_injections_v2.instructions");

            if (instructions != null)
            {
                this.Set(GetTimelineEntries(body));
            }
            else
            {
                this.Set(Enumerable.Empty<TimelineEntry>());
            }

        }

        private IEnumerable<TimelineEntry> GetTimelineEntries(JToken instructions)
        {
            foreach (var instruction in instructions)
            {
                var instructionType = instruction.Value<string>("type");

                if (string.Equals(instructionType, "TimelineAddEntries"))
                {
                    foreach (var entry in instruction.Value<JArray>("entries"))
                    {
                        yield return new TimelineEntry(entry);
                    }

                }
                else if (string.Equals(instructionType, "TimelineReplaceEntry"))
                {
                    yield return new TimelineEntry(instruction.Value<JToken>("entry"));
                }

            }

        }

        public void Set(IEnumerable<TimelineEntry> entries)
        {
            lock (this.Entires)
            {
                this.Entires.Clear();
                this.Entires.AddRange(entries);
                this.ResetEvent.Set();
            }

        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            this.ResetEvent.DisposeQuietly();
        }

    }

}
