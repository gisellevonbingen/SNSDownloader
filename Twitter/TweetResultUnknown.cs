using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class TweetResultUnknown : TweetResult
    {
        public JToken Json { get; }

        public TweetResultUnknown()
        {
        }

        public TweetResultUnknown(JToken json) : base(json)
        {
            this.Json = json;
        }

    }

}
