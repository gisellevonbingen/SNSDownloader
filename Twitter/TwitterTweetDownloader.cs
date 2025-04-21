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
        public static Regex TweetDetailPattern { get; } = TwitterUtils.GetGraphqlPattern("TweetDetail");
        public static Regex SrcPattern { get; } = new Regex("src=\"(?<src>[^\"]+)\"");
        public static Regex SizePattern { get; } = new Regex("(?<width>\\d+)x(?<height>\\d+)");


        private readonly AutoResetEvent ResetEvent;

        private JObject Data;

        public TwitterTweetDownloader()
        {
            this.ResetEvent = new AutoResetEvent(false);

            this.Data = null;
        }

        public override string PlatformName => "TwitterTweet";

        public override void OnNetworkCreated(INetwork network)
        {
            network.NetworkResponseReceived += this.OnNetworkResponseReceived;
        }

        protected override void OnReset()
        {
            this.ResetEvent.Reset();

            this.Data = null;
        }

        public override bool Test(string url) => this.Test(url, out _);

        public bool Test(string url, out string id)
        {
            id = TwitterUtils.GetTweetId(url);
            return !string.IsNullOrEmpty(id);
        }

        protected override bool OnReady(string url) => true;

        public override DownloadResult Download(DownloadOutput output)
        {
            if (!this.WaitAll(this.ResetEvent))
            {
                return DownloadResult.Failed;
            }
            else if (this.Exception != null)
            {
                throw new Exception(string.Empty, this.Exception);
            }
            else if (this.Data == null)
            {
                this.Log("Not found");
                return DownloadResult.Failed;
            }
            else
            {
                var id = TwitterUtils.GetTweetId(this.Url);

                this.Log($"Found");
                return this.DownloadTweet(output, id);
            }

        }

        private DownloadResult DownloadTweet(DownloadOutput output, string tweetId)
        {
            var entires = new List<TimelineEntry>();
            var errors = this.Data.Value<JArray>("errors");

            if (errors != null)
            {
                if (errors.Count == 1 && errors[0].Value<string>("message") == "_Missing: No status found with that ID.")
                {
                    this.Log("Deleted");
                    return DownloadResult.Deleted;
                }
                else
                {
                    throw new Exception($"Error: {errors}");
                }

            }

            var instructions = this.Data.SelectToken("data.threaded_conversation_with_injections_v2.instructions");

            if (instructions != null)
            {
                entires.AddRange(TwitterUtils.GetTimelineEntries(instructions));
            }
            else
            {
                throw new NullReferenceException(nameof(instructions));
            }

            var results = entires.Select(i => i.Content).OfType<TimelineEntryContentItem>().Select(item => item.Result).Where(t => t != null).ToArray();
            var found = results.OfType<TweetResultTweet>().FirstOrDefault(i => i.Id.Equals(tweetId));
            var createdAt = found.CreatedAt.ToLocalTime();
            var directory = Path.Combine(output.Directory, found.User.ScreenName, $"{createdAt:yyyy}", $"{createdAt.ToYearMonthString()}");
            Directory.CreateDirectory(directory);

            var tweetPrefix = $"{createdAt.ToFileNameString()}_{found.User.ScreenName}_{tweetId}";

            File.WriteAllText(Path.Combine(directory, $"{tweetPrefix}.json"), $"{this.Data}");
            this.DownloadMedia(tweetPrefix, directory, results);

            return DownloadResult.Success;
        }

        private void DownloadMedia(string tweetPrefix, string directory, IEnumerable<TweetResult> results)
        {
            var downladIndex = 0;
            var rrr = new List<TweetResult>(results);

            for (var i = 0; i < rrr.Count; i++)
            {
                var result = rrr[i];

                if (result is TweetResultTweet tweet)
                {
                    //if (!string.IsNullOrEmpty(tweet.QuotedUrl) && !(tweet.QuotedResult == null || tweet.QuotedResult is TweetResultTombstone))
                    //{
                    //this.Children.Add(tweet.QuotedUrl);
                    //}

                    if (tweet.QuotedResult is TweetResultTweet quoted)
                    {
                        rrr.Add(quoted);
                    }

                    if (tweet.ReweetedResult is TweetResultTweet retweeted)
                    {
                        rrr.Add(retweeted);
                    }

                    this.ProcessCard(tweet);

                    foreach (var url in tweet.Urls)
                    {
                        if (url.ExpandedUrl == null)
                        {

                        }
                        else if (url.ExpandedUrl.StartsWith("http://twitpic.com", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            tweet.Media.Add(new MediaEntityTwitPic() { Url = url.ExpandedUrl });
                        }

                    }

                    this.Log($"Media found : {tweet.Media.Count}");

                    foreach (var media in tweet.Media)
                    {
                        var mediaFilePrefix = $"{tweetPrefix}_{++downladIndex}";
                        this.DownloadMedia(directory, mediaFilePrefix, media);
                    }

                }

            }

        }

        private void ProcessCard(TweetResultTweet tweet)
        {
            var card = tweet.Card;

            if (card == null)
            {
                return;
            }

            var split = card.Name.Split(':');

            if (split.Length > 2)
            {
                throw new Exception($"Unknown card name: {card.Name}");
            }

            var type = split.Length == 2 ? split[1] : split[0];
            var title = card.BindingValues.TryGetValue("title", out var jTitle) ? jTitle.Value<string>("string_value") : string.Empty;

            if (type.Equals("summary"))
            {

            }
            else if (type.Equals("summary_large_image"))
            {

            }
            else if (TweetResultTweet.PollTextOnlyPattern.TryMatch(type, out var pollMatch))
            {

            }
            else if (type.Equals("promo_image_convo"))
            {
                tweet.Media.Add(new MediaEntityBlob() { Url = card.BindingValues["promo_image"].SelectToken("image_value.url").Value<string>() });
            }
            else if (type.Equals("player"))
            {

            }
            else if (type.Equals("live_event"))
            {

            }
            else if (type.Equals("audiospace"))
            {
                var cardUrl = card.BindingValues["card_url"].Value<string>("string_value");
                var audioSpaceUrl = this.ReplaceUrls(tweet, cardUrl);
                AudioSpaceResult result = null;
                this.Log($"AudioSpace Request: {audioSpaceUrl}");

                if (!Program.Operate(Program.TwitterSpaceDownloader, audioSpaceUrl, d => d.TryGetResult(out result)) || result == null)
                {
                    throw new Exception($"AudioSpace Not Found");
                }
                else
                {
                    this.Log($"AudioSpace Found");
                    tweet.Media.Add(new MediaEntityAudioSpace() { Url = audioSpaceUrl, Result = result });
                }

            }
            else if (type.Equals("unified_card"))
            {
                var t = card.BindingValues["unified_card"];
                var type2 = t.Value<string>("type");

                if (type2.Equals("STRING", StringComparison.OrdinalIgnoreCase))
                {
                    var t2 = t.Value<string>("string_value");
                    var wrapped = JObject.Parse(t2);
                    var entities = wrapped.Value<JObject>("media_entities");

                    foreach (var pair in entities)
                    {
                        tweet.Media.Add(TweetResultTweet.ParseMedia(pair.Value));
                    }

                }

            }
            else
            {
                throw new Exception($"Unknown card name: {card.Name}");
            }

        }

        private string ReplaceUrls(TweetResultTweet tweet, string text)
        {
            text = tweet.Urls.Aggregate(text, (t, url) => t.Replace(url.Url, url.ExpandedUrl));
            text = tweet.Media.OfType<MediaEntityTwitter>().Aggregate(text, (t, m) => t.Replace($" {m.Url}", m.MediaUrl));
            return text;
        }

        private string DownloadMedia(string directory, string mediaFilePrefix, MediaEntity media)
        {
            if (media is MediaEntityBlob blob)
            {
                return this.DownloadBlob(directory, mediaFilePrefix, blob.Url);
            }
            else if (media is MediaEntityTwitterPhoto photo)
            {
                return this.DownloadBlob(directory, mediaFilePrefix, $"{photo.MediaUrl}?name=large");
            }
            else if (media is MediaEntityTwitPic twitpic)
            {
                using var page = Program.CreateRequest(twitpic.Url).GetWrappedResponse();

                if (page.Success == true)
                {
                    var html = page.Response.ReadAsString(Program.UTF8WithoutBOM);
                    var groups = SrcPattern.Match(html).Groups;

                    this.DownloadBlob(directory, mediaFilePrefix, groups["src"].Value);
                }

                return twitpic.Url;
            }
            else if (media is MediaEntityTwitterVideo video)
            {
                var downloadTupleList = video.VideoInfo.Variants.SelectMany(v => this.GetVideoDownloadTuples(video, v)).ToArray();

                foreach (var type in new MediaDownloadData.DownloadType[] { MediaDownloadData.DownloadType.Blob, MediaDownloadData.DownloadType.M3U })
                {
                    foreach (var tuple in downloadTupleList.Where(d => d.Download.Type == type).OrderByDescending(o => o.Bitrate))
                    {
                        var path = Program.GetMediaFilePath(directory, mediaFilePrefix, new Uri(tuple.Download.Url));
                        Program.DownloadMedia(path, tuple.Download);
                        return tuple.Download.Url;
                    }

                }

                return this.DownloadBlob(directory, mediaFilePrefix, $"{video.MediaUrl}?name=large");
            }
            else if (media is MediaEntityAudioSpace audioSpace)
            {
                var sourceLocation = audioSpace.Result.LiveVideoStream.SelectToken("source.location").Value<string>();
                var downloadPath = Program.GetMediaFilePath(directory, mediaFilePrefix, new Uri(sourceLocation));

                using var ms = new MemoryStream(Program.UTF8WithoutBOM.GetBytes(audioSpace.Result.ToJson().ToString()));
                Program.DownloadBlob(Path.ChangeExtension(downloadPath, ".space"), ms);

                Program.DownloadMedia(downloadPath, new MediaDownloadData()
                {
                    Type = MediaDownloadData.DownloadType.M3U,
                    Url = sourceLocation,
                });

                return sourceLocation;
            }
            else
            {
                throw new Exception($"Unknown Media Type: {media}");
            }

        }

        private string DownloadBlob(string directory, string mediaFilePrefix, string url)
        {
            using var response = Program.CreateRequest(url).GetWrappedResponse();

            if (response.Success == true)
            {
                Program.DownloadBlob(directory, mediaFilePrefix, response.Response);
            }

            return url;
        }

        private IEnumerable<(MediaDownloadData Download, int Bitrate)> GetVideoDownloadTuples(MediaEntityTwitterVideo video, VideoVariant variant)
        {
            if (variant.ContentType.Equals("video/mp4") == true)
            {
                yield return (new MediaDownloadData() { Type = MediaDownloadData.DownloadType.Blob, Url = variant.Url }, variant.Bitrate);
            }
            else if (variant.ContentType.Equals("application/x-mpegURL") == true)
            {
                yield return (new MediaDownloadData() { Type = MediaDownloadData.DownloadType.M3U, Url = variant.Url }, 0);
            }
            else
            {
                throw new Exception($"Unknown ContentType: {variant.ContentType}");
            }

        }

        private void OnNetworkResponseReceived(object sender, NetworkResponseReceivedEventArgs e)
        {
            if (!this.IsReady)
            {
                return;
            }
            else if (TweetDetailPattern.IsMatch(e.ResponseUrl) == false || string.IsNullOrEmpty(e.ResponseBody) == true)
            {
                return;
            }

            try
            {
                this.Data = JObject.Parse(e.ResponseBody);

            }
            catch (Exception ex)
            {
                this.Exception = ex;
            }

            this.ResetEvent.Set();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            this.ResetEvent.DisposeQuietly();
        }

    }

}
