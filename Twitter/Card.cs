using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class Card
    {
        public Dictionary<string, JToken> BindingValues { get; } = new Dictionary<string, JToken>();
        public string Url { get; set; } = string.Empty;

        public Card()
        {

        }

        public Card(JToken json)
        {
            var legacy = json.Value<JToken>("legacy");

            foreach (var pair in legacy.Value<JArray>("binding_values") ?? Enumerable.Empty<JToken>())
            {
                var key = pair.Value<string>("key");
                var value = pair.Value<JToken>("value");
                this.BindingValues[key] = value;
            }

            this.Url = legacy.Value<string>("url");
        }

    }

}
