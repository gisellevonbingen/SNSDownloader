using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class MediaEntityTwitterVideo : MediaEntityTwitter
    {
        public VideoInfo VideoInfo { get; set; } = new VideoInfo();

        public MediaEntityTwitterVideo()
        {

        }

        public MediaEntityTwitterVideo(JToken json) : base(json)
        {
            this.VideoInfo = new VideoInfo(json.Value<JObject>("video_info"));
        }

    }

}
