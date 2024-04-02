using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SNSDownloader.Twitter
{
    public class MediaEntityTwitterPhoto : MediaEntityTwitter
    {
        public MediaEntityTwitterPhoto()
        {

        }

        public MediaEntityTwitterPhoto(JToken json) : base(json)
        {

        }

    }

}
