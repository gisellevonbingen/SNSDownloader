using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class AudioSpaceResult
    {
        public JToken Metadata { get; set; } = null;
        public JObject LiveVideoStream { get; set; } = null;

        public JToken ToJson() => new JObject
        {
            ["metadata"] = this.Metadata,
            ["live_video_stream"] = this.LiveVideoStream
        };

    }

}
