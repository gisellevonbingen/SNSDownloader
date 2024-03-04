using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SNSDownloader
{
    public class WrappedResponse : IDisposable
    {
        public HttpWebRequest Request { get; }
        public HttpWebResponse Response { get; }
        public bool Success { get; }

        public WrappedResponse(HttpWebRequest request, HttpWebResponse response, bool success)
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

        ~WrappedResponse()
        {
            this.Dispose(false);
        }

    }

}
