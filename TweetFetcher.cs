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

        public event EventHandler<string> Requested;

        public TweetFetcher()
        {
            this.List = new List<TwitterTwitInfo>();
            this.ResetEvent = new AutoResetEvent(false);
        }

        public void Enqueue(TwitterTwitInfo tweet)
        {
            lock (this.List)
            {
                this.List.Add(tweet);
            }

        }

        public void Set()
        {
            this.ResetEvent.Set();
        }

        public IEnumerable<TwitterTwitInfo> Fetch(string url)
        {
            lock (this.List)
            {
                this.List.Clear();

                this.Requested?.Invoke(this, url);
            }

            this.ResetEvent.WaitOne();

            lock (this.List)
            {
                foreach (var tweet in this.List)
                {
                    yield return tweet;
                }

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
