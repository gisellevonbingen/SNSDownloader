using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace TwitterVideoDownloader
{
    public class TweetFetcher : IDisposable
    {
        private readonly List<TwitterTwitInfo> List;
        private readonly AutoResetEvent ResetEvent;

        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(1.0D);

        public event EventHandler<string> Requested;

        public TweetFetcher()
        {
            this.List = new List<TwitterTwitInfo>();
            this.ResetEvent = new AutoResetEvent(false);
        }

        public void Set(IEnumerable<TwitterTwitInfo> tweets)
        {
            lock (this.List)
            {
                this.ResetEvent.Set();
                this.List.Clear();
                this.List.AddRange(tweets);
            }

        }

        public IEnumerable<TwitterTwitInfo> Fetch(string url)
        {
            this.Requested?.Invoke(this, url);

            if (this.ResetEvent.WaitOne(this.Timeout) == true)
            {
                lock (this.List)
                {
                    foreach (var tweet in this.List)
                    {
                        yield return tweet;
                    }

                    this.List.Clear();
                }

            }
            else
            {
                Console.WriteLine("Fetch timeout");
            }

        }

        protected virtual void Dispose(bool disposing)
        {
            this.ResetEvent.Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.Dispose(true);
        }

        ~TweetFetcher()
        {
            this.Dispose(false);
        }

    }

}
