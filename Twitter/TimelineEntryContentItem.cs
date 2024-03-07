using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class TimelineEntryContentItem : TimelineEntryContent
    {
        public TimelineTweet Tweet { get; set; } = null;

        public TimelineEntryContentItem()
        {

        }

        public TimelineEntryContentItem(JToken json)
        {
            var itemContent = json.Value<JObject>("itemContent");
            var itemType = itemContent.Value<string>("itemType");

            if (string.Equals(itemType, "TimelineTweet"))
            {
                this.Tweet = new TimelineTweet(itemContent);
            }

        }

    }

}
