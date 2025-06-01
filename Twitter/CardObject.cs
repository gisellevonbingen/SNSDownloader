using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class CardObject
    {
        public string Type { get; set; }
        public JToken Data { get; set; }

        public CardObject(JToken json)
        {
            this.Type = json.Value<string>("type");
            this.Data = json.Value<JToken>("data");
        }

    }

}
