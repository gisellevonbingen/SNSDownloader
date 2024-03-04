using System;
using System.Collections.Generic;
using System.Text;

namespace SNSDownloader
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
