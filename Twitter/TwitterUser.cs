using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SNSDownloader.Twitter
{
    public class TwitterUser
    {
        public string ScreenName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public TwitterUser()
        {

        }

        public TwitterUser(JToken json) : this()
        {
            this.ScreenName = json.Value<string>("screen_name");
            this.Name = json.Value<string>("name");
        }

    }

}
