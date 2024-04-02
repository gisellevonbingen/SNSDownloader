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
        public string Quoted { get; set; } = string.Empty;
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
            var user = json.SelectToken("core.user_results.result.legacy");

            this.Id = core.Value<string>("id_str");
            this.User = new LegacyUserData(user);
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
                this.Quoted = quoted.Value<string>("expanded");
            }

            var mediaArray = core.SelectToken("extended_entities.media");

            if (mediaArray != null)
            {
                foreach (var media in mediaArray)
                {
                    var mediaType = media.Value<string>("type");

                    if (string.Equals(mediaType, "photo"))
                    {
                        this.Media.Add(new MediaEntityTwitterPhoto(media));
                    }
                    else if (string.Equals(mediaType, "video"))
                    {
                        this.Media.Add(new MediaEntityTwitterVideo(media));
                    }
                    else if (string.Equals(mediaType, "animated_gif"))
                    {
                        this.Media.Add(new MediaEntityTwitterVideo(media));
                    }
                    else
                    {
                        throw new Exception($"Unknown media type: {mediaType}");
                    }

                }

            }

        }

    }

}
