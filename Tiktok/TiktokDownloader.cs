using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;

namespace SNSDownloader.Tiktok
{
    public class TiktokDownloader : AbstractDownloader
    {
        public static Regex ArticlePattern { get; } = new Regex("https:\\/\\/www\\.tiktok\\.com/(?<user_id>.+)/video/(?<article_id>.+)");

        public object SyncRoot { get; } = new object();

        private readonly AutoResetEvent ItemResetEvent;
        private readonly AutoResetEvent VideoResetEvent;

        private JToken Item;
        private WrappedResponse Video;

        public TiktokDownloader()
        {
            this.ItemResetEvent = new AutoResetEvent(false);
            this.VideoResetEvent = new AutoResetEvent(false);

            this.Item = null;
            this.Video = null;
        }

        public override string PlatformName => "TikTok";

        public override void OnNetworkCreated(INetwork network)
        {
            network.NetworkRequestSent += this.OnNetworkRequestSent;
            network.NetworkResponseReceived += this.OnNetworkResponseReceived;
        }

        public override void Reset()
        {
            this.Item = null;
            this.Video.DisposeQuietly();
            this.Video = null;
            this.ItemResetEvent.Reset();
            this.VideoResetEvent.Reset();
        }

        public override bool Test(string url) => this.Test(url, out _);

        public bool Test(string url, out string id)
        {
            id = ArticlePattern.Match(url).Groups["article_id"].Value;
            return !string.IsNullOrEmpty(id);
        }

        public override bool Download(string url, string baseDirectory)
        {
            if (!this.Test(url, out var id))
            {
                return false;
            }
            else if (!this.WaitAll(this.ItemResetEvent, this.VideoResetEvent))
            {
                return false;
            }
            else if (this.Item == null || this.Video == null)
            {
                this.Log("Not found");
                return false;
            }
            else
            {
                this.Log($"Found");

                var dateTime = DateTimeOffset.UnixEpoch.Add(TimeSpan.FromSeconds(this.Item["createTime"].Value<int>())).ToLocalTime();
                var authorId = this.Item["author"]["uniqueId"].Value<string>();
                var authorNickname = this.Item["author"]["nickname"].Value<string>();

                var fileprefix = $"{dateTime:yyyyMMdd_HHmmss}_{authorId}_{id}";

                var directory = Path.Combine(baseDirectory, $"{dateTime.ToLocalTime():yyyy-MM}");
                Directory.CreateDirectory(directory);

                using var fs = new FileStream(Path.Combine(directory, $"{fileprefix}.txt"), FileMode.Create);
                using var wrier = new StreamWriter(fs, Program.UTF8WithoutBOM);
                wrier.WriteLine($"CreatedAt: {dateTime:yyyy-MM-dd HH:mm:ss}");
                wrier.WriteLine($"Url: {url}");
                wrier.WriteLine($"User: {authorNickname}(@{authorId})");
                wrier.WriteLine($"Id: {id}");
                wrier.WriteLine();
                wrier.WriteLine(this.Item["desc"].Value<string>());

                Program.DownloadSimpleMedia(Path.Combine(directory, $"{fileprefix}.mp4"), this.Video.Response);
                return true;
            }

        }

        private void OnNetworkRequestSent(object sender, NetworkRequestSentEventArgs e)
        {
            if (e.RequestUrl.StartsWith("https://v16-webapp-prime.tiktok.com/video"))
            {
                var response = Program.GetResponse(e);
                this.SetVideo(response);
            }

        }

        private void OnNetworkResponseReceived(object sender, NetworkResponseReceivedEventArgs e)
        {
            if (ArticlePattern.IsMatch(e.ResponseUrl) == true)
            {
                var document = new HtmlDocument();
                document.LoadHtml(e.ResponseBody);
                var json = JObject.Parse(document.DocumentNode.SelectSingleNode("//*[@id=\"__UNIVERSAL_DATA_FOR_REHYDRATION__\"]").InnerText);
                var item = json["__DEFAULT_SCOPE__"]["webapp.video-detail"]["itemInfo"]["itemStruct"];
                this.SetItem(item);
            }

        }

        public void SetItem(JToken itme)
        {
            lock (this.SyncRoot)
            {
                this.Item = itme;
                this.ItemResetEvent.Set();
            }

        }

        public void SetVideo(WrappedResponse video)
        {
            lock (this.SyncRoot)
            {
                this.Video = video;
                this.VideoResetEvent.Set();
            }

        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            this.VideoResetEvent.DisposeQuietly();
        }

    }

}
