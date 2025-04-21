using System;
using System.Collections.Generic;
using System.Text;

namespace SNSDownloader
{
    public enum DownloadResult : byte
    {
        Success = 0,
        Deleted = 1,
        Failed = 2,
    }

}
