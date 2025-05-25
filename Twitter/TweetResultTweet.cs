using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SNSDownloader.Util;

namespace SNSDownloader.Twitter
{
    public class TweetResultTweet : TweetResult
    {
        public static Regex PollTextOnlyPattern { get; } = new Regex("poll(?<poll>\\d+)choice_text_only");

        public string Id { get; set; } = string.Empty;
        public LegacyUserData User { get; set; } = new LegacyUserData();
        public DateTime CreatedAt { get; set; }
        public string QuotedUrl { get; set; } = string.Empty;
        public TweetResult QuotedResult { get; set; } = null;
        public TweetResult ReweetedResult { get; set; } = null;
        public string FullText { get; set; } = string.Empty;
        public List<UrlData> Urls { get; } = new List<UrlData>();
        public List<MediaEntity> Media { get; } = new List<MediaEntity>();
        public Card Card { get; set; } = null;

        public TweetResultTweet()
        {

        }

        public TweetResultTweet(JToken json) : base(json)
        {
            var core = json.SelectToken("legacy");
            var userResult = json.SelectToken("core.user_results.result");

            this.Id = core.Value<string>("id_str");

            var userCore = userResult.Value<JToken>("core");
            var userLegacy = userResult.Value<JToken>("legacy");
            this.User = new LegacyUserData(userCore ?? userLegacy);

            if (this.User.ScreenName == null || this.User.Name == null)
            {
                throw new Exception($"Unknown user data: {userResult}");
            }

            this.CreatedAt = DateTime.ParseExact(core.Value<string>("created_at"), "ddd MMM dd HH:mm:ss K yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            this.FullText = core.Value<string>("full_text");

            var jcard = json.Value<JToken>("card");

            if (jcard != null)
            {
                this.Card = new Card(jcard);
            }

            var urlArray = core.SelectToken("entities.urls");

            if (urlArray != null)
            {
                this.Urls.AddRange(urlArray.Select(url => new UrlData(url)));
            }

            var quoted = core.SelectToken("quoted_status_permalink");

            if (quoted != null)
            {
                this.QuotedUrl = quoted.Value<string>("expanded");
                this.QuotedResult = TimelineEntryContentItem.GetTimelineTweet(json.SelectToken("quoted_status_result.result"));
            }

            var retweeted = core.SelectToken("retweeted_status_result");

            if (retweeted != null)
            {
                this.ReweetedResult = TimelineEntryContentItem.GetTimelineTweet(core.SelectToken("retweeted_status_result.result"));
            }

            var mediaArray = core.SelectToken("extended_entities.media");

            if (mediaArray != null)
            {
                foreach (var media in mediaArray)
                {
                    this.Media.Add(ParseMedia(media));
                }

            }

        }

        public static MediaEntityTwitter ParseMedia(JToken media)
        {
            var mediaType = media.Value<string>("type");

            if (string.Equals(mediaType, "photo"))
            {
                return new MediaEntityTwitterPhoto(media);
            }
            else if (string.Equals(mediaType, "video"))
            {
                return new MediaEntityTwitterVideo(media);
            }
            else if (string.Equals(mediaType, "animated_gif"))
            {
                return new MediaEntityTwitterVideo(media);
            }
            else
            {
                throw new Exception($"Unknown media type: {mediaType}");
            }

        }

    }

}
