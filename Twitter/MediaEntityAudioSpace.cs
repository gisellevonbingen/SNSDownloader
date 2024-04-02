using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class MediaEntityAudioSpace : MediaEntity
    {
        public string SourceLocation { get; set; } = string.Empty;

        public MediaEntityAudioSpace()
        {

        }

    }

}
