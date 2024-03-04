using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class TwitterUrl
    {
        public string DisplayUrl { get; set; } = string.Empty;
        public string ExpandedUrl { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int Index0 { get; set; } = 0;
        public int Index1 { get; set; } = 0;

        public TwitterUrl()
        {

        }

        public TwitterUrl(JToken json) : this()
        {
            this.DisplayUrl = json.Value<string>("display_url");
            this.ExpandedUrl = json.Value<string>("expanded_url");
            this.Url = json.Value<string>("url");
            var indices = json.Value<JArray>("indices").Values<int>().ToArray();
            this.Index0 = indices[0];
            this.Index1 = indices[1];
        }

    }

}
