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
            return entryType switch
            {
                "TimelineTimelineItem" => new TimelineEntryContentItem(content),
                "TimelineTimelineCursor" => new TimelineEntryContentCursor(content),
                "TimelineTimelineModule" => new TimelineEntryContentUnknown(content),
                _ => throw new Exception($"Unknown entryType: {entryType}"),
            };
        }

    }

}
