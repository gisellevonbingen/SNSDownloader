using System;
using System.Collections.Generic;
using System.Text;

namespace SNSDownloader.Twitter
{
    public class TwitterTwitInfo
    {
        public string Id { get; set; } = string.Empty;
        public TwitterUser User { get; set; } = new TwitterUser();
        public DateTime CreatedAt { get; set; }
        public string Quoted { get; set; } = string.Empty;
        public string FullText { get; set; } = string.Empty;
        public List<TwitterUrl> Url { get; } = new List<TwitterUrl>();
        public List<TwitterMediaEntity> Media { get; } = new List<TwitterMediaEntity>();

        public TwitterTwitInfo()
        {

        }

    }

}
