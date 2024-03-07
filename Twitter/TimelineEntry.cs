using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class TimelineEntry
    {
        public string Id { get; set; } = string.Empty;
        public long SortIndex { get; set; } = 0L;
        public TimelineEntryContent Content { get; set; } = null;

        public TimelineEntry()
        {

        }

        public TimelineEntry(JToken json)
        {
            this.Id = json.Value<string>("entryId");
            this.SortIndex = json.Value<long>("sortIndex");
            this.Content = GetContent(json.Value<JObject>("content"));
        }

        public static TimelineEntryContent GetContent(JToken content)
        {
            var entryType = content?.Value<string>("entryType");

            if (string.Equals(entryType, "TimelineTimelineItem") == true)
            {
                return new TimelineEntryContentItem(content);
            }
            else if (string.Equals(entryType, "TimelineTimelineCursor"))
            {
                return new TimelineEntryContentCursor(content);
            }
            else
            {
                throw new Exception($"Unknown entryType: {entryType}");
            }

        }

    }

}
