﻿using System;
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

        public override bool Download(DownloadOutput output)
        {
            if (!this.WaitAll(this.ResetEvent))
            {
                return false;
            }
            else if (this.Exception != null)
            {
                throw new Exception(string.Empty, this.Exception);
            }
            else if (this.Data == null)
            {
                this.Log("Not found");
                return false;
            }
            else
            {
                var id = TwitterUtils.GetTweetId(this.Url);

                this.Log($"Found");
                this.DownloadTweet(output, id);
                return true;
            }

        }

        private void DownloadTweet(DownloadOutput output, string tweetId)
        {
            var entires = new List<TimelineEntry>();
            var errors = this.Data.Value<JToken>("errors");

            if (errors != null)
            {
                throw new Exception($"{errors}");
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
            var directory = Path.Combine(output.Directory, found.User.ScreenName, $"{createdAt.ToYearMonthString()}");
            Directory.CreateDirectory(directory);

            var tweetPrefix = $"{createdAt.ToFileNameString()}_{found.User.ScreenName}_{tweetId}";

            var downladIndex = 0;
            File.WriteAllText(Path.Combine(directory, $"{tweetPrefix}.json"), $"{this.Data}");

            var mediaUrls = new Dictionary<string, string>();

            foreach (var result in results)
            {
                if (result is TweetResultTweet tweet)
                {
                    this.ProcessCard(tweet);
                    tweet.FullText = this.ReplaceUrls(tweet, tweet.FullText);

                    foreach (var url in tweet.Urls)
                    {
                        if (url.ExpandedUrl.StartsWith("http://twitpic.com", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            tweet.Media.Add(new MediaEntityTwitPic() { Url = url.ExpandedUrl });
                        }

                    }

                    this.Log($"Media found : {tweet.Media.Count}");

                    foreach (var media in tweet.Media)
                    {
                        if (mediaUrls.ContainsKey(media.Url) == true)
                        {
                            continue;
                        }

                        var mediaFilePrefix = $"{tweetPrefix}_{++downladIndex}";
                        var downloadedUrl = this.DownloadMedia(directory, mediaFilePrefix, media);
                        mediaUrls[media.Url] = downloadedUrl;
                    }

                }

            }

        }

        private void Downlaod(string directory, string tweetPrefix, TweetResult[] results)
        {
            var downladIndex = 0;
            using var tweetStream = new FileStream(Path.Combine(directory, $"{tweetPrefix}.txt"), FileMode.Create);
            using var tweetWriter = new StreamWriter(tweetStream, Program.UTF8WithoutBOM);
            var mediaUrls = new Dictionary<string, string>();

            for (var ti = 0; ti < results.Length; ti++)
            {
                var result = results[ti];

                if (ti > 0)
                {
                    tweetWriter.WriteLine();
                    tweetWriter.WriteLine(new string('=', 40));
                    tweetWriter.WriteLine();
                }

                if (result is TweetResultTweet tweet)
                {
                    this.ProcessCard(tweet);
                    tweet.FullText = this.ReplaceUrls(tweet, tweet.FullText);

                    foreach (var url in tweet.Urls)
                    {
                        if (url.ExpandedUrl.StartsWith("http://twitpic.com", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            tweet.Media.Add(new MediaEntityTwitPic() { Url = url.ExpandedUrl });
                        }

                    }

                    this.Log($"Media found : {tweet.Media.Count}");

                    foreach (var media in tweet.Media)
                    {
                        if (mediaUrls.ContainsKey(media.Url) == true)
                        {
                            continue;
                        }

                        var mediaFilePrefix = $"{tweetPrefix}_{++downladIndex}";
                        var downloadedUrl = this.DownloadMedia(directory, mediaFilePrefix, media);
                        mediaUrls[media.Url] = downloadedUrl;
                    }

                    this.WriteTweet(tweetWriter, tweet, mediaUrls);
                }
                else if (result is TweetResultTombstone tombstone)
                {
                    tweetWriter.WriteLine("$Tombstone$");
                }
                else
                {
                    throw new Exception($"Unknown {nameof(TweetResult)}: {result}");
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
                var builder = new StringBuilder($"{title}{Environment.NewLine}");

                if (card.BindingValues.TryGetValue("description", out var description))
                {
                    builder.Append($"{description.Value<string>("string_value")}{Environment.NewLine}");
                }

                this.PatchCardText(tweet, $"{builder}");
            }
            else if (type.Equals("summary_large_image"))
            {
                this.PatchCardText(tweet, title);
            }
            else if (TweetResultTweet.PollTextOnlyPattern.TryMatch(type, out var pollMatch))
            {
                var poll = int.Parse(pollMatch.Groups["poll"].Value);
                var list = new List<KeyValuePair<string, int>>();

                for (var i = 0; i < poll; i++)
                {
                    var label = card.BindingValues[$"choice{i + 1}_label"].Value<string>("string_value");
                    var count = int.Parse(card.BindingValues[$"choice{i + 1}_count"].Value<string>("string_value"));
                    list.Add(KeyValuePair.Create(label, count));
                }

                var totalCount = list.Sum(i => new int?(i.Value)) ?? 0;
                var builder = new StringBuilder();

                foreach (var (label, count) in list)
                {
                    builder.AppendLine($"{label}: {count}({count / (totalCount / 100.0F):F2}%)");
                }

                builder.Append($"Total: {totalCount}");
                this.PatchCardText(tweet, $"{builder}");
            }
            else if (type.Equals("promo_image_convo"))
            {
                this.PatchCardText(tweet, title);
                tweet.Media.Add(new MediaEntityBlob() { Url = card.BindingValues["promo_image"].SelectToken("image_value.url").Value<string>() });
            }
            else if (type.Equals("player"))
            {

            }
            else if (type.Equals("live_event"))
            {
                var eventTitle = card.BindingValues["event_title"].Value<string>("string_value");
                this.PatchCardText(tweet, eventTitle);
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
                    this.PatchCardText(tweet, result.AudioSpace.Title);
                    tweet.Media.Add(new MediaEntityAudioSpace() { Url = audioSpaceUrl, SourceLocation = result.SourceLocation });
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

        private void PatchCardText(TweetResultTweet tweet, string text)
        {
            var card = tweet.Card;

            if (card == null)
            {
                return;
            }

            var fullUrl = card.BindingValues.TryGetValue("card_url", out var jCardUrl) ? jCardUrl.Value<string>("string_value") : string.Empty;
            tweet.FullText = $"{$"```{card.Name}: {fullUrl}{Environment.NewLine}{text}{Environment.NewLine}```"}{Environment.NewLine}{tweet.FullText}";
        }

        private void WriteTweet(TextWriter tweetWriter, TweetResultTweet tweet, Dictionary<string, string> mediaMap)
        {
            tweetWriter.WriteLine($"CreatedAt: {tweet.CreatedAt.ToStandardString()}");
            tweetWriter.WriteLine($"Url: {TwitterUtils.GetStatusUrl(tweet)}");
            tweetWriter.WriteLine($"User: {tweet.User.Name}(@{tweet.User.ScreenName})");
            tweetWriter.WriteLine($"Id: {tweet.Id}");
            tweetWriter.WriteLine($"Quoted: {tweet.Quoted}");
            tweetWriter.WriteLine($"Media: {tweet.Media.Count}");

            for (var mi = 0; mi < tweet.Media.Count; mi++)
            {
                tweetWriter.WriteLine($"- {mediaMap[tweet.Media[mi].Url]}");
            }

            tweetWriter.WriteLine();
            tweetWriter.WriteLine(HttpUtility.HtmlDecode(tweet.FullText));
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
                        var path = Program.GetMediaFilePath(directory, mediaFilePrefix, tuple.Download.Url);
                        Program.DownloadMedia(path, tuple.Download);
                        return tuple.Download.Url;
                    }

                }

                return this.DownloadBlob(directory, mediaFilePrefix, $"{video.MediaUrl}?name=large");
            }
            else if (media is MediaEntityAudioSpace audioSpace)
            {
                Program.DownloadMedia(Program.GetMediaFilePath(directory, mediaFilePrefix, audioSpace.SourceLocation), new MediaDownloadData()
                {
                    Type = MediaDownloadData.DownloadType.M3U,
                    Url = audioSpace.SourceLocation,
                });
                return audioSpace.SourceLocation;
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
                this.Log($"Unknown ContentType: {variant.ContentType}");
            }

        }

        private bool TryParseSize(string url, out Size size)
        {
            var match = SizePattern.Match(url);

            if (match.Success)
            {
                var groups = match.Groups;
                var width = int.Parse(groups["width"].Value);
                var height = int.Parse(groups["height"].Value);
                size = new Size(width, height);
                return true;
            }
            else
            {
                size = default;
                return false;
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
