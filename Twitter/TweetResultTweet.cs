using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class TweetResultTweet : TweetResult
    {
        public const string MarshmallowSuffix = " | マシュマロ";

        public string Id { get; set; } = string.Empty;
        public UserData User { get; set; } = new UserData();
        public DateTime CreatedAt { get; set; }
        public string Quoted { get; set; } = string.Empty;
        public string FullText { get; set; } = string.Empty;
        public List<UrlData> Url { get; } = new List<UrlData>();
        public List<MediaEntity> Media { get; } = new List<MediaEntity>();

        public TweetResultTweet()
        {

        }

        public TweetResultTweet(JToken json) : base(json)
        {
            var core = json.SelectToken("legacy");
            var user = json.SelectToken("core.user_results.result.legacy");

            this.Id = core.Value<string>("id_str");
            this.User = new UserData(user);
            this.CreatedAt = DateTime.ParseExact(core.Value<string>("created_at"), "ddd MMM dd HH:mm:ss K yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            this.FullText = core.Value<string>("full_text");

            var jcard = json.Value<JToken>("card");

            if (jcard != null)
            {
                var card = new Card(jcard);
                var title = card.BindingValues["title"].Value<string>("string_value");

                if (title.EndsWith(MarshmallowSuffix))
                {
                    title = title[..^MarshmallowSuffix.Length];
                }

                this.FullText = $"{$"```marshmallow{Environment.NewLine}{title}{Environment.NewLine}```"}{Environment.NewLine}{this.FullText.Replace(card.Url, "")}";
            }

            var urlArray = core.SelectToken("entities.urls");

            if (urlArray != null)
            {
                foreach (var url in urlArray)
                {
                    var turl = new UrlData(url);
                    this.Url.Add(turl);
                }

                foreach (var url in this.Url)
                {
                    this.FullText = this.FullText.Replace(url.Url, url.ExpandedUrl);

                    if (url.ExpandedUrl.StartsWith("http://twitpic.com", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        this.Media.Add(new MediaEntityTwitPic() { Url = url.ExpandedUrl });
                    }

                }

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

                    if (string.Equals(mediaType, "photo") == true)
                    {
                        this.Media.Add(new MediaEntityPhoto(media) { Large = true });
                    }
                    else if (string.Equals(mediaType, "video") == true)
                    {
                        this.Media.Add(new MediaEntityVideo(media));
                    }

                }

            }

        }

    }

}
