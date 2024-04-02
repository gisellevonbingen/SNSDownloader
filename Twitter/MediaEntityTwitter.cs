using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public abstract class MediaEntityTwitter : MediaEntity
    {
        public string MediaUrl { get; set; } = string.Empty;

        public MediaEntityTwitter()
        {

        }

        public MediaEntityTwitter(JToken json)
        {
            this.Url = json.Value<string>("url");
            this.MediaUrl = json.Value<string>("media_url_https");
        }

    }

}
