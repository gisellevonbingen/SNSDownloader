using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SNSDownloader.Twitter
{
    public class MediaEntityTwitterPhoto : MediaEntity
    {
        public MediaEntityTwitterPhoto()
        {

        }

        public MediaEntityTwitterPhoto(JToken json) : this()
        {
            this.Url = $"{json.Value<string>("media_url_https")}?name=large";
        }

    }

}
