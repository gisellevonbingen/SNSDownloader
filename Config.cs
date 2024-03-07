using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SNSDownloader
{
    public class Config
    {
        public string FFmpegPath { get; set; } = string.Empty;

        public Config()
        {

        }

        public void Load(JToken json)
        {
            this.FFmpegPath = json.Value<string>("FFmpegPath") ?? this.FFmpegPath;
        }

        public void Save(JToken json)
        {
            json["FFmpegPath"] = this.FFmpegPath;
        }

    }

}
