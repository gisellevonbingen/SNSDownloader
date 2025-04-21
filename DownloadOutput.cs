using System;
using System.Collections.Generic;
using System.Text;

namespace SNSDownloader
{
    public class DownloadOutput
    {
        public UrlCollection Progressed { get; }
        public string Directory { get; }

        public DownloadOutput(UrlCollection progressed, string directory)
        {
            this.Progressed = progressed;
            this.Directory = directory;
        }

    }

}
