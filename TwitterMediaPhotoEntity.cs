using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace TwitterVideoDownloader
{
    public class TwitterMediaPhotoEntity : TwitterMediaEntity
    {
        public bool Large { get; set; } = false;

        public TwitterMediaPhotoEntity()
        {

        }

        public TwitterMediaPhotoEntity(JToken json) : this()
        {
            this.Url = json.Value<string>("media_url_https");
        }

        public string RequestUrl => this.Large ? $"{this.Url}?name=large" : this.Url;

    }

}
