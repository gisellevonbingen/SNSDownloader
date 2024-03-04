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

        private readonly List<TwitterTwitInfo> List;
        private readonly AutoResetEvent ResetEvent;

        public TwitterDownloader()
        {
            this.List = new List<TwitterTwitInfo>();
            this.ResetEvent = new AutoResetEvent(false);
        }

        public override string PlatformName => "Twitter";

        public override void OnNetworkCreated(INetwork network)
        {
            network.NetworkResponseReceived += this.OnNetowrkResponseReceived;
        }

        public override void Reset()
        {
            this.List.Clear();
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
            else if (this.List.Count == 0)
            {
                this.Log("Not found");
                return false;
            }
            else
            {
                this.Log($"Found : {this.List.Count}");
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
            var found = this.List.FirstOrDefault(i => i.Id.Equals(tweetId));
            var directory = Path.Combine(baseDirectory, found.User.ScreenName, $"{found.CreatedAt.ToLocalTime():yyyy-MM}");
            Directory.CreateDirectory(directory);

            var tweetPrefix = $"{found.CreatedAt.ToLocalTime():yyyyMMdd_HHmmss}_{found.User.ScreenName}_{tweetId}";
            var downladIndex = 0;
            using var tweetStream = new FileStream(Path.Combine(directory, $"{tweetPrefix}.txt"), FileMode.Create);
            using var tweetWriter = new StreamWriter(tweetStream, Program.UTF8WithoutBOM);
            var mediaUrls = new HashSet<string>();

            for (var ti = 0; ti < this.List.Count; ti++)
            {
                var tweet = this.List[ti];
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

        private void WriteTweet(TextWriter tweetWriter, TwitterTwitInfo tweet)
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

        private void DownloadMedia(string directory, string mediaFilePrefix, TwitterMediaEntity media)
        {
            if (media is TwitterMediaPhotoEntity photo)
            {
                using var response = Program.GetResponse(photo.RequestUrl);

                if (response.Success == true)
                {
                    Program.DownloadSimpleMedia(directory, mediaFilePrefix, response.Response);
                }

            }
            else if (media is TwitterMediaTwitPicEntity twitpic)
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
            else if (media is TwitterMediaVideoEntity video)
            {
                var variants = video.VideoInfo.Variants;
                var downloadList = variants.SelectMany(v => this.GetDownloadDataList(video, v)).ToArray();
                Program.DownloadLargest(directory, mediaFilePrefix, downloadList);

            }

        }

        private IEnumerable<DownloadData> GetDownloadDataList(TwitterMediaVideoEntity video, TwitterVideoVariant variant)
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
            this.Set(GetTweets(body));
        }

        private IEnumerable<TwitterTwitInfo> GetTweets(JObject body)
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
                    if (this.TryParseTweetFromTimelineEntry(entry, out var tweet) == true)
                    {
                        yield return tweet;
                    }

                }

            }

        }

        private bool TryParseTweetFromTimelineEntry(JToken entry, out TwitterTwitInfo tweet)
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

        public void Set(IEnumerable<TwitterTwitInfo> tweets)
        {
            lock (this.List)
            {
                this.List.Clear();
                this.List.AddRange(tweets);
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
