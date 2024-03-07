using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using SNSDownloader.Net;
using SNSDownloader.Util;

namespace SNSDownloader.Twitter
{
    public class TwitterTweetDownloader : AbstractDownloader
    {
        public static Regex TweetDetailPattern { get; } = new Regex("https:\\/\\/twitter\\.com\\/i\\/api\\/graphql\\/.+\\/TweetDetail");
        public static Regex SrcPattern { get; } = new Regex("src=\"(?<src>[^\"]+)\"");
        public static Regex SizePattern { get; } = new Regex("(?<width>\\d+)x(?<height>\\d+)");


        private readonly List<TimelineEntry> Entires;
        private readonly AutoResetEvent ResetEvent;

        public TwitterTweetDownloader()
        {
            this.Entires = new List<TimelineEntry>();
            this.ResetEvent = new AutoResetEvent(false);
        }

        public override string PlatformName => "TwitterTweet";

        public override void OnNetworkCreated(INetwork network)
        {
            network.NetworkResponseReceived += this.OnNetowrkResponseReceived;
        }

        protected override void OnReset()
        {
            this.Entires.Clear();
            this.ResetEvent.Reset();
        }

        public override bool Test(string url) => this.Test(url, out _);

        public bool Test(string url, out string id)
        {
            id = TwitterUtils.GetTweetId(url);
            return !string.IsNullOrEmpty(id);
        }

        protected override bool OnReady(string url) => true;

        public override bool Download(DownloadOutput output)
        {
            if (!this.WaitAll(this.ResetEvent))
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
                var id = TwitterUtils.GetTweetId(this.Url);

                this.Log($"Found : {this.Entires.Count}");
                this.DownloadTweet(id, output.Directory);
                return true;
            }

        }

        private void DownloadTweet(string tweetId, string baseDirectory)
        {
            var tweets = this.Entires.Select(i => i.Content).OfType<TimelineEntryContentItem>().Select(item => item.Tweet).Where(t => t != null).ToArray();
            var found = tweets.FirstOrDefault(i => i.Id.Equals(tweetId));
            var createdAt = found.CreatedAt.ToLocalTime();
            var directory = Path.Combine(baseDirectory, found.User.ScreenName, $"{createdAt.ToYearMonthString()}");
            Directory.CreateDirectory(directory);

            var tweetPrefix = $"{createdAt.ToFileNameString()}_{found.User.ScreenName}_{tweetId}";
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
            tweetWriter.WriteLine($"CreatedAt: {tweet.CreatedAt.ToStandardString()}");
            tweetWriter.WriteLine($"Url: {TwitterUtils.GetStatusUrl(tweet)}");
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
                using var response = Program.CreateRequest(photo.RequestUrl).GetWrappedResponse();

                if (response.Success == true)
                {
                    Program.Download(directory, mediaFilePrefix, response.Response);
                }

            }
            else if (media is MediaEntityTwitPic twitpic)
            {
                using var page = Program.CreateRequest(twitpic.Url).GetWrappedResponse();

                if (page.Success == true)
                {
                    var html = page.Response.ReadAsString(Program.UTF8WithoutBOM);
                    var groups = SrcPattern.Match(html).Groups;
                    var src = groups["src"].Value;

                    using var response = Program.CreateRequest(src).GetWrappedResponse();

                    if (response.Success == true)
                    {
                        Program.Download(directory, mediaFilePrefix, response.Response);
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

        private IEnumerable<MediaDownloadData> GetDownloadDataList(MediaEntityVideo video, VideoVariant variant)
        {
            if (variant.ContentType.Equals("video/mp4") == true)
            {
                yield return new MediaDownloadData() { Type = MediaDownloadData.DownloadType.Blob, Url = variant.Url, Size = this.ParseSize(variant.Url) };
            }
            else if (variant.ContentType.Equals("application/x-mpegURL") == true)
            {
                yield return new MediaDownloadData() { Type = MediaDownloadData.DownloadType.M3U, Url = variant.Url, Size = video.OriginalSize };
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
            if (!this.IsReady)
            {
                return;
            }
            else if (TweetDetailPattern.IsMatch(e.ResponseUrl) == false || string.IsNullOrEmpty(e.ResponseBody) == true)
            {
                return;
            }

            var body = JObject.Parse(e.ResponseBody);
            var instructions = body.SelectToken("data.threaded_conversation_with_injections_v2.instructions");

            if (instructions != null)
            {
                this.Set(TwitterUtils.GetTimelineEntries(instructions));
            }
            else
            {
                this.Set(Enumerable.Empty<TimelineEntry>());
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
