using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using OpenQA.Selenium;

namespace SNSDownloader
{
    public abstract class AbstractDownloader : IDownloader, IDisposable
    {
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(1.0D);

        public abstract string PlatformName { get; }

        public abstract void OnNetworkCreated(INetwork network);

        public abstract void Reset();

        public abstract bool Test(string url);

        public abstract bool Download(string url, string outputDirectory);

        public void Log(string message) => Console.WriteLine($"[{this.PlatformName}] {message}");

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
