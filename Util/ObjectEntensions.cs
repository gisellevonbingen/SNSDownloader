using System;
using System.Collections.Generic;
using System.Text;

namespace SNSDownloader.Util
{
    public static class ObjectEntensions
    {
        public static bool DisposeQuietly(this IDisposable disposable)
        {
            if (disposable != null)
            {
                try
                {
                    disposable.Dispose();
                    return true;
                }
                catch
                {

                }

            }

            return false;
        }

    }

}
