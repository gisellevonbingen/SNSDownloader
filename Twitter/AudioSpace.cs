using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class AudioSpace
    {
        public DateTime CreatedAt { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public LegacyUserData Creator { get; set; } = new LegacyUserData();
        public TweetResult Tweet { get; set; } = new TweetResultTombstone();

        public string MediaKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        public AudioSpace()
        {

        }

        public AudioSpace(JToken metadata) : this()
        {
            this.CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(metadata.Value<long>("created_at")).LocalDateTime;
            this.StartedAt = DateTimeOffset.FromUnixTimeMilliseconds(metadata.Value<long>("started_at")).LocalDateTime;
            this.EndedAt = DateTimeOffset.FromUnixTimeMilliseconds(metadata.Value<long>("ended_at")).LocalDateTime;
            this.UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(metadata.Value<long>("updated_at")).LocalDateTime;

            this.Creator = new LegacyUserData(metadata.SelectToken("creator_results.result.legacy"));
            this.Tweet = TimelineEntryContentItem.GetTimelineTweet(metadata.SelectToken("tweet_results.result"));

            this.MediaKey = metadata.Value<string>("media_key");
            this.Title = metadata.Value<string>("title");
        }

    }

}
