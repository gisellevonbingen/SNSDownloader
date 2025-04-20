using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using SNSDownloader.Util;

namespace SNSDownloader
{
    public class ProgressTracker : IDisposable
    {
        public string Path { get; private set; }

        private readonly FileSystemWatcher Watcher;
        private readonly HashSet<string> Set;

        private readonly object ReloadLock = new object();
        private bool Locking = false;

        public ProgressTracker(string path)
        {
            this.Path = path;

            this.Watcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileName(path));
            this.Watcher.EnableRaisingEvents = true;
            this.Watcher.Changed += this.OnWatcherChanged;

            this.Set = new HashSet<string>();
            this.Reload();
        }

        private void Reload()
        {
            try
            {
                if (!File.Exists(this.Path))
                {
                    return;
                }

                var lines = File.ReadAllLines(this.Path).ToArray();

                lock (this.ReloadLock)
                {
                    this.Set.Clear();

                    foreach (var line in lines)
                    {
                        this.Set.Add(line);
                    }

                }

                Console.WriteLine("ProgressTracker Reloaded");
            }
            catch
            {

            }

        }

        private void OnWatcherChanged(object sender, FileSystemEventArgs e)
        {
            lock (this.ReloadLock)
            {
                if (this.Locking)
                {
                    this.Locking = false;
                    return;
                }

                Thread.Sleep(100);
                this.Reload();
            }

        }

        private static string Replace(string value)
        {
            return value.Replace("twitter.com", "x.com");
        }

        public bool Contains(string value)
        {
            lock (this.ReloadLock)
            {
                return this.Set.Contains(Replace(value));
            }

        }

        public bool Add(string value)
        {
            lock (this.ReloadLock)
            {
                this.Locking = true;
                File.AppendAllLines(this.Path, new[] { Replace(value) });
                return this.Set.Add(value);
            }

        }

        protected virtual void Dispose(bool disposing)
        {
            this.Watcher.DisposeQuietly();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.Dispose(true);
        }

        ~ProgressTracker()
        {
            this.Dispose(false);
        }

    }

}
