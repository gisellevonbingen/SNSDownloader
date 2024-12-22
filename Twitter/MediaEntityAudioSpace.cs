using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class MediaEntityAudioSpace : MediaEntity
    {
        public AudioSpaceResult Result { get; set; } = null;

        public MediaEntityAudioSpace()
        {

        }

    }

}
