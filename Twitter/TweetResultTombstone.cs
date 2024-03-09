using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class TweetResultTombstone : TweetResult
    {
        public TweetResultTombstone()
        {

        }

        public TweetResultTombstone(JToken json) : base(json)
        {

        }

    }

}
