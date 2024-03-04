using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace SNSDownloader
{
    public class DownloadData
    {
        public Size Size { get; set; }
        public List<Uri> Segments { get; } = new List<Uri>();

        public DownloadData()
        {

        }

        public DownloadData(params Uri[] segments)
        {
            this.Segments.AddRange(segments);
        }

        public DownloadData(IEnumerable<Uri> segments)
        {
            this.Segments.AddRange(segments);
        }

    }

}
