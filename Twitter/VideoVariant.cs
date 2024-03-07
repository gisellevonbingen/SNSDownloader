﻿using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class VideoVariant
    {
        public int Bitrate { get; set; } = 0;
        public string ContentType { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;

        public VideoVariant()
        {

        }

        public VideoVariant(JToken json) : this()
        {
            this.Bitrate = json.Value<int>("bitrate");
            this.ContentType = json.Value<string>("content_type");
            this.Url = json.Value<string>("url");
        }

        public JObject ToJson() => new JObject()
        {
            ["bitrate"] = this.Bitrate,
            ["content_type"] = this.ContentType,
            ["url"] = this.Url,
        };

    }

}
