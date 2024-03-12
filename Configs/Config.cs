using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SNSDownloader.Configs
{
    public class Config
    {
        public string FFmpegPath { get; set; }
        public bool LogSkipped { get; set; }
        public TwitterConfig Twitter { get; set; }

        public Config(JToken json)
        {
            this.FFmpegPath = json.Value<string>("FFmpegPath") ?? string.Empty;
            this.LogSkipped = json.Value<bool?>("LogSkipped") ?? true;
            this.Twitter = new TwitterConfig(json.Value<JObject>("Twitter") ?? new JObject());
        }

        public void Save(JToken json)
        {
            json["FFmpegPath"] = this.FFmpegPath;
            json["LogSkipped"] = this.LogSkipped;
            this.Twitter.Save(json["Twitter"] = new JObject());
        }

    }

}
