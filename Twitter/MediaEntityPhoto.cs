using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SNSDownloader.Twitter
{
    public class MediaEntityPhoto : MediaEntity
    {
        public bool Large { get; set; } = false;

        public MediaEntityPhoto()
        {

        }

        public MediaEntityPhoto(JToken json) : this()
        {
            this.Url = json.Value<string>("media_url_https");
        }

        public string RequestUrl => this.Large ? $"{this.Url}?name=large" : this.Url;

    }

}
