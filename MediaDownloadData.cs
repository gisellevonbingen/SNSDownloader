using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace SNSDownloader
{
    public class MediaDownloadData
    {
        public Size Size { get; set; }
        public List<Uri> Segments { get; } = new List<Uri>();

        public MediaDownloadData()
        {

        }

        public MediaDownloadData(params Uri[] segments)
        {
            this.Segments.AddRange(segments);
        }

        public MediaDownloadData(IEnumerable<Uri> segments)
        {
            this.Segments.AddRange(segments);
        }

    }

}
