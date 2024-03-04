using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class TwitterMediaVideoEntity : TwitterMediaEntity
    {
        public Dictionary<string, SizeData> Sizes { get; } = new Dictionary<string, SizeData>();
        public Size OriginalSize { get; set; } = new Size();
        public TwitterVideoInfo VideoInfo { get; set; } = null;

        public TwitterMediaVideoEntity()
        {

        }

        public TwitterMediaVideoEntity(JToken json) : this()
        {
            this.Url = json.Value<string>("media_url_https");
            
            foreach (var pair in json.Value<JObject>("sizes"))
            {
                this.Sizes[pair.Key] = new SizeData(pair.Value);
            }

            var original_info = json.Value<JObject>("original_info");
            this.OriginalSize = new Size(original_info.Value<int>("width"), original_info.Value<int>("height"));
            this.VideoInfo = new TwitterVideoInfo(json.Value<JObject>("video_info"));
        }

    }

}
