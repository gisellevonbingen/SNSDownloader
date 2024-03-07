using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using SNSDownloader.Util;

namespace SNSDownloader.Net
{
    public class WrappedHttpResponse : IDisposable
    {
        public HttpWebRequest Request { get; }
        public HttpWebResponse Response { get; }
        public bool Success { get; }

        public WrappedHttpResponse(HttpWebRequest request, HttpWebResponse response, bool success)
        {
            this.Request = request;
            this.Response = response;
            this.Success = success;
        }

        protected virtual void Dispose(bool disposing)
        {
            this.Response.DisposeQuietly();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.Dispose(true);
        }

        ~WrappedHttpResponse()
        {
            this.Dispose(false);
        }

    }

}
