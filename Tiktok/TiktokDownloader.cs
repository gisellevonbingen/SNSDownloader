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

        private readonly AutoResetEvent ItemResetEvent;
        private readonly AutoResetEvent VideoResetEvent;

        private bool Deleted;
        private JToken Item;
        private string[] VideoUrls;
        private WrappedHttpResponse Video;

        public TiktokDownloader()
        {
            this.ItemResetEvent = new AutoResetEvent(false);
            this.VideoResetEvent = new AutoResetEvent(false);

            this.Deleted = false;
            this.Item = null;
            this.VideoUrls = null;
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
            this.Deleted = false;
            this.Item = null;
            this.VideoUrls = null;
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

        public override DownloadResult Download(DownloadOutput output)
        {
            if (this.WaitAll(this.ItemResetEvent))
            {
                if (this.Exception != null)
                {
                    throw new Exception(string.Empty, this.Exception);
                }
                else if (this.Deleted)
                {
                    this.Log("Deleted");
                    return DownloadResult.Deleted;
                }

            }
            else
            {
                return DownloadResult.Failed;
            }

            if (this.WaitAll(this.VideoResetEvent))
            {
                if (this.Exception != null)
                {
                    throw new Exception(string.Empty, this.Exception);
                }

            }
            else
            {
                return DownloadResult.Failed;
            }

            
            if (this.Item == null || this.Video == null)
            {
                this.Log("Not found");
                return DownloadResult.Failed;
            }
            else
            {
                this.Log($"Found completed");

                var url = this.Url;
                var id = this.GetArticleId(url);

                var dateTime = DateTimeOffset.FromUnixTimeSeconds(this.Item["createTime"].Value<int>()).LocalDateTime;
                var authorId = this.Item["author"]["uniqueId"].Value<string>();

                var fileprefix = $"{dateTime.ToFileNameString()}_{authorId}_{id}";

                var directory = Path.Combine(output.Directory, $"{dateTime:yyyy}", $"{dateTime.ToYearMonthString()}");
                Directory.CreateDirectory(directory);

                File.WriteAllText(Path.Combine(directory, $"{fileprefix}.json"), $"{this.Item}");
                Program.DownloadBlob(Path.Combine(directory, $"{fileprefix}.mp4"), this.Video.Response);
                return DownloadResult.Success;
            }

        }

        private void OnNetworkRequestSent(object sender, NetworkRequestSentEventArgs e)
        {
            if (!this.IsReady)
            {
                return;
            }
            else if (this.VideoUrls != null && this.VideoUrls.Contains(e.RequestUrl))
            {
                try
                {
                    this.Video = Program.CreateRequest(e).GetWrappedResponse();
                    this.Log($"Found Video");
                }
                catch (Exception ex)
                {
                    this.Exception = ex;
                }

                this.VideoResetEvent.Set();
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
                    var detail = json["__DEFAULT_SCOPE__"]["webapp.video-detail"];

                    if (detail.Value<string>("statusMsg") == "status_deleted")
                    {
                        this.Deleted = true;
                        this.ItemResetEvent.Set();
                        this.VideoResetEvent.Set();
                        return;
                    }

                    this.Item = detail.SelectToken("itemInfo.itemStruct");
                    var size = this.Item.SelectToken("video.size").Value<int>();
                    var bitrateInfo = this.Item.SelectToken("video.bitrateInfo").Where(j => !j.Value<string>("GearName").Contains("adapt") && j.SelectToken("PlayAddr.DataSize").Value<int>() == size).FirstOrDefault();
                    this.VideoUrls = bitrateInfo.SelectToken("PlayAddr.UrlList").Values<string>().ToArray();
                    this.Log($"Found Item");
                }
                catch (Exception ex)
                {
                    this.Exception = ex;
                }

                this.ItemResetEvent.Set();
            }

        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            this.VideoResetEvent.DisposeQuietly();
        }

    }

}
