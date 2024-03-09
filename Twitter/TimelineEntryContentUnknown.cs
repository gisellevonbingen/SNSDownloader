using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class TimelineEntryContentUnknown : TimelineEntryContent
    {
        public JToken Content { get; set; }

        public TimelineEntryContentUnknown()
        {
        }

        public TimelineEntryContentUnknown(JToken content) : base(content)
        {
            this.Content = content;
        }

    }

}
