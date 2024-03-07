using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class TimelineEntryContentCursor : TimelineEntryContent
    {
        public string Value { get; set; } = string.Empty;
        public string CursorType { get; set; } = string.Empty;

        public TimelineEntryContentCursor()
        {

        }

        public TimelineEntryContentCursor(JToken json) : base(json)
        {
            this.Value = json.Value<string>("value");
            this.CursorType = json.Value<string>("cursorType");
        }

    }

}
