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
            var itemContent = content.Value<JObject>("itemContent");
            var itemType = itemContent.Value<string>("itemType");

            if (string.Equals(itemType, "TimelineTweet"))
            {
                this.Result = GetTimelineTweet(itemContent);
            }
            else if (string.Equals(itemType, "TimelineTimelineCursor"))
            {

            }
            else
            {
                throw new Exception($"Unknown itemType: {itemType}");
            }

        }

        public static TweetResult GetTimelineTweet(JToken itemContent)
        {
            var result = itemContent.SelectToken("tweet_results.result");
            var typeName = result.Value<string>("__typename");
            return typeName switch
            {
                "Tweet" => new TweetResultTweet(result),
                "TweetWithVisibilityResults" => new TweetResultTweet(result["tweet"]),
                "TweetTombstone" => new TweetResultTombstone(result),
                _ => throw new ArgumentOutOfRangeException($"Unknown Typename: {typeName}")
            };

        }

    }

}
