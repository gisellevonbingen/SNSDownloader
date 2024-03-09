using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SNSDownloader
{
    public class Config
    {
        public string FFmpegPath { get; set; } = string.Empty;
        public bool LogSkipped { get; set; } = true;

        public Config()
        {

        }

        public void Load(JToken json)
        {
            this.FFmpegPath = json.Value<string>("FFmpegPath") ?? this.FFmpegPath;
            this.LogSkipped = json.Value<bool?>("LogSkipped") ?? this.LogSkipped;
        }

        public void Save(JToken json)
        {
            json["FFmpegPath"] = this.FFmpegPath;
            json["LogSkipped"] = this.LogSkipped;
        }

    }

}
