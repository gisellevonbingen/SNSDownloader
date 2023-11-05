using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace TwitterVideoDownloader
{
    public class TwitterMediaPhotoEntity : TwitterMediaEntity
    {
        public string Url { get; set; } = string.Empty;

        public TwitterMediaPhotoEntity()
        {

        }

        public TwitterMediaPhotoEntity(JToken json) : this()
        {
            this.Url = json.Value<string>("media_url_https");
        }


    }

}
