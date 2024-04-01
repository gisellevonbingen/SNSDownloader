using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using OpenQA.Selenium;

namespace SNSDownloader
{
    public abstract class AbstractDownloader : IDisposable
    {
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(1.0D);

        public bool IsReady { get; private set; }
        protected string Url { get; private set; }
        protected Exception Exception { get; set; }

        public abstract string PlatformName { get; }

        public abstract void OnNetworkCreated(INetwork network);

        public void Reset()
        {
            this.IsReady = false;
            this.Url = string.Empty;
            this.Exception = null;
            this.OnReset();
        }

        protected abstract void OnReset();

        public abstract bool Test(string url);

        public bool Ready(string url)
        {
            if (!this.Test(url))
            {
                return false;
            }
            else if (this.OnReady(url))
            {
                this.IsReady = true;
                this.Url = url;
                return true;
            }
            else
            {
                return false;
            }

        }

        protected abstract bool OnReady(string url);

        public abstract bool Download(DownloadOutput output);

        public virtual string GetRequestUrl() => this.Url;

        public void Log() => Log(string.Empty);

        public void Log<T>(T message) => Console.WriteLine($"[{this.PlatformName}] {message}");

        protected virtual void Dispose(bool disposing)
        {
            this.Reset();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.Dispose(true);
        }

        ~AbstractDownloader()
        {
            this.Dispose(false);
        }

    }

}
