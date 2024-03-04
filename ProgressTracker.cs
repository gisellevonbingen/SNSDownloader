using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SNSDownloader
{
    public class ProgressTracker
    {
        public string Path { get; private set; }
        private readonly HashSet<string> Set;

        public ProgressTracker(string path)
        {
            this.Path = path;
            this.Set = new HashSet<string>();

            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    this.Set.Add(line);
                }

            }

        }

        public bool Contains(string value) => this.Set.Contains(value);

        public bool Add(string value)
        {
            File.AppendAllLines(this.Path, new[] { value });
            return this.Set.Add(value);
        }

    }

}
