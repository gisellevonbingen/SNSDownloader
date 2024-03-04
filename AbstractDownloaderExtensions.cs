using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SNSDownloader
{
    public static class AbstractDownloaderExtensions
    {
        public static bool WaitAll(this AbstractDownloader fetcher, params WaitHandle[] waitHandles)
        {
            foreach (var waitHandle in waitHandles)
            {
                if (!waitHandle.WaitOne(fetcher.Timeout))
                {
                    fetcher.Log("Timeout");
                    return false;
                }

            }

            return true;
        }

    }

}
