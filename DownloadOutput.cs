using System;
using System.Collections.Generic;
using System.Text;

namespace SNSDownloader
{
    public class DownloadOutput
    {
        public ProgressTracker Progressed { get; }
        public string Directory { get; }

        public DownloadOutput(ProgressTracker progressed, string directory)
        {
            this.Progressed = progressed;
            this.Directory = directory;
        }

    }

}
