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
using SNSDownloader.Net;
using SNSDownloader.Util;

namespace SNSDownloader.Tiktok
{
    public class TiktokDownloader : AbstractDownloader
    {
        public static Regex ArticlePattern { get; } = new Regex("https:\\/\\/www\\.tiktok\\.com/(?<user_id>.+)/video/(?<article_id>.+)");

        public object SyncRoot { get; } = new object();

        private readonly AutoResetEvent ItemResetEvent;
        private readonly AutoResetEvent VideoResetEvent;

        private JToken Item;
        private WrappedHttpResponse Video;

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

        protected override void OnReset()
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
            id = this.GetArticleId(url);
            return !string.IsNullOrEmpty(id);
        }

        public string GetArticleId(string url) => ArticlePattern.Match(url).Groups["article_id"].Value;

        protected override bool OnReady(string url) => true;


        public override bool Download(DownloadOutput output)
        {
            if (!this.WaitAll(this.ItemResetEvent, this.VideoResetEvent))
            {
                return false;
            }
            else if (this.Exception != null)
            {
                throw new Exception(string.Empty, this.Exception);
            }
            else if (this.Item == null || this.Video == null)
            {
                this.Log("Not found");
                return false;
            }
            else
            {
                this.Log($"Found");

                var url = this.Url;
                var id = this.GetArticleId(url);

                var dateTime = DateTimeOffset.UnixEpoch.Add(TimeSpan.FromSeconds(this.Item["createTime"].Value<int>())).LocalDateTime;
                var authorId = this.Item["author"]["uniqueId"].Value<string>();
                var authorNickname = this.Item["author"]["nickname"].Value<string>();

                var fileprefix = $"{dateTime.ToFileNameString()}_{authorId}_{id}";

                var directory = Path.Combine(output.Directory, $"{dateTime.ToYearMonthString()}");
                Directory.CreateDirectory(directory);

                using var fs = new FileStream(Path.Combine(directory, $"{fileprefix}.txt"), FileMode.Create);
                using var wrier = new StreamWriter(fs, Program.UTF8WithoutBOM);
                wrier.WriteLine($"CreatedAt: {dateTime.ToStandardString()}");
                wrier.WriteLine($"Url: {url}");
                wrier.WriteLine($"User: {authorNickname}(@{authorId})");
                wrier.WriteLine($"Id: {id}");
                wrier.WriteLine();
                wrier.WriteLine(this.Item["desc"].Value<string>());

                Program.DownloadBlob(Path.Combine(directory, $"{fileprefix}.mp4"), this.Video.Response);
                return true;
            }

        }

        private void OnNetworkRequestSent(object sender, NetworkRequestSentEventArgs e)
        {
            if (!this.IsReady)
            {
                return;
            }
            else if (e.RequestUrl.StartsWith("https://v16-webapp-prime.tiktok.com/video"))
            {
                try
                {
                    var response = Program.CreateRequest(e).GetWrappedResponse();
                    this.SetVideo(response);
                }
                catch (Exception ex)
                {
                    this.Exception = ex;
                    this.SetVideo(null);
                }

            }

        }

        private void OnNetworkResponseReceived(object sender, NetworkResponseReceivedEventArgs e)
        {
            if (!this.IsReady)
            {
                return;
            }
            else if (ArticlePattern.IsMatch(e.ResponseUrl) == true)
            {
                try
                {
                    var document = new HtmlDocument();
                    document.LoadHtml(e.ResponseBody);
                    var json = JObject.Parse(document.DocumentNode.SelectSingleNode("//*[@id=\"__UNIVERSAL_DATA_FOR_REHYDRATION__\"]").InnerText);
                    var item = json["__DEFAULT_SCOPE__"]["webapp.video-detail"]["itemInfo"]["itemStruct"];
                    this.SetItem(item);
                }
                catch (Exception ex)
                {
                    this.Exception = ex;
                    this.SetItem(null);
                }

            }

        }

        private void SetItem(JToken itme)
        {
            lock (this.SyncRoot)
            {
                this.Item = itme;
                this.ItemResetEvent.Set();
            }

        }

        public void SetVideo(WrappedHttpResponse video)
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
