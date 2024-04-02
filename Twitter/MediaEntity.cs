using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SNSDownloader.Twitter
{
    public abstract class MediaEntity
    {
        public string Url { get; set; } = string.Empty;

        public MediaEntity()
        {

        }

    }

}
