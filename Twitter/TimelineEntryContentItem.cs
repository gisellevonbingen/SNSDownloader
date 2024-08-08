using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class TimelineEntryContentItem : TimelineEntryContent
    {
        public TweetResult Result { get; set; } = null;

        public TimelineEntryContentItem()
        {

        }

        public TimelineEntryContentItem(JToken content)
        {
            this.Result = GetTimelineItem(content);
        }

        public static TweetResult GetTimelineItem(JToken content)
        {
            var itemContent = content.Value<JObject>("itemContent");
            var itemType = itemContent.Value<string>("itemType");
            return itemType switch
            {
                "TimelineTweet" => GetTimelineTweet(itemContent.SelectToken("tweet_results.result")),
                "TimelineTombstone" => new TweetResultTombstone(itemContent),
                "TimelineTimelineCursor" => null,
                _ => throw new Exception($"Unknown itemType: {itemType}"),
            };
        }

        public static TweetResult GetTimelineTweet(JToken result)
        {
            if (result == null)
            {
                return null;
            }

            var typeName = result.Value<string>("__typename");
            return typeName switch
            {
                "Tweet" => new TweetResultTweet(result),
                "TweetWithVisibilityResults" => new TweetResultTweet(result["tweet"]),
                "TweetTombstone" => new TweetResultTombstone(result),
                _ => throw new ArgumentOutOfRangeException($"Unknown Typename: {typeName}"),
            };
        }

    }

}
