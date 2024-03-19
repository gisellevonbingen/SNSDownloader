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

                if (card.Name.Equals("summary"))
                {
                    var domain = card.BindingValues["domain"].Value<string>("string_value");
                    var title = card.BindingValues["title"].Value<string>("string_value");
                    var builder = new StringBuilder($"```{domain}{Environment.NewLine}{title}{Environment.NewLine}");

                    if (card.BindingValues.TryGetValue("description", out var description))
                    {
                        builder.Append($"{description.Value<string>("string_value")}{Environment.NewLine}");
                    }

                    this.FullText = builder.Append($"```{Environment.NewLine}{this.FullText}").ToString();
                }
                else if (card.Name.Equals("summary_large_image"))
                {
                    var domain = card.BindingValues["domain"].Value<string>("string_value");
                    var title = card.BindingValues["title"].Value<string>("string_value");
                    this.FullText = $"{$"```{domain}{Environment.NewLine}{title}{Environment.NewLine}```"}{Environment.NewLine}{this.FullText.Replace(card.Url, "")}";
                }
                else if (PollTextOnlyPattern.TryMatch(card.Name, out var pollMatch))
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

                    this.FullText = $"{$"```poll{Environment.NewLine}{builder}{Environment.NewLine}Total: {totalCount}{Environment.NewLine}```"}{Environment.NewLine}{this.FullText}";
                }
                else if (card.Name.Equals("player"))
                {

                }
                else
                {
                    throw new Exception($"Unknown card name: {card.Name}");
                }

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
