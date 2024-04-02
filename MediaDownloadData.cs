using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace SNSDownloader
{
    public class MediaDownloadData
    {
        public DownloadType Type { get; set; }
        public string Url { get; set; }

        public MediaDownloadData()
        {

        }

        public enum DownloadType
        {
            Blob = 0,
            M3U = 1,
        }

    }

}
