using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using SNSDownloader.Util;

namespace SNSDownloader.Tiktok
{
    public class TiktokUserDownloader : AbstractDownloader
    {
        public const string Prefix = "TiktokUser:";

        private NetworkResponseReceivedEventArgs FirstResponse;
        private string UserName;
        private readonly AutoResetEvent ResetEvent;

        public TiktokUserDownloader()
        {
            this.ResetEvent = new AutoResetEvent(false);
        }

        public override string PlatformName => "TiktokUser";

        public override void OnNetworkCreated(INetwork network)
        {
            network.NetworkResponseReceived += this.OnNetworkResponseReceived;
        }

        protected override void OnReset()
        {
            this.FirstResponse = null;
            this.UserName = null;
            this.ResetEvent.Reset();
        }

        public override bool Test(string url) => url.StartsWith(Prefix);

        public override string GetRequestUrl() => $"https://www.tiktok.com/@{this.UserName}";

        protected override bool OnReady(string url)
        {
            this.UserName = url[Prefix.Length..];
            return true;
        }

        public override DownloadResult Download(DownloadOutput output)
        {
            if (!this.WaitAll(this.ResetEvent))
            {
                return DownloadResult.Failed;
            }
            else if (this.Exception != null)
            {
                throw new Exception(string.Empty, this.Exception);
            }
            else if (this.FirstResponse == null)
            {
                this.Log("Not found");
                return DownloadResult.Failed;
            }
            else
            {
                this.Log($"Found");

                var json = JObject.Parse(this.FirstResponse.ResponseBody);
                var itemList = json.Value<JArray>("itemList");
                var path = Path.Combine(output.Directory, $"{string.Concat(this.UserName.Split(Path.GetInvalidFileNameChars()))}_{DateTime.Now.ToFileNameString()}.json");
                var videoUrls = this.GetVideoUrls(output, itemList).ToArray();

                if (videoUrls.Length > 0)
                {
                    File.WriteAllText(path, new JArray(videoUrls).ToString());
                }

                return DownloadResult.Success;
            }

        }

        public override bool CanSkip => false;

        private IEnumerable<string> GetVideoUrls(DownloadOutput output, JArray itemList)
        {
            foreach (var item in itemList)
            {
                var id = item.Value<string>("id");
                var videoUrl = $"https://www.tiktok.com/@{this.UserName}/video/{id}";

                if (!output.Progressed.Contains(videoUrl))
                {
                    yield return videoUrl;
                }

            }

        }

        private void OnNetworkResponseReceived(object sender, NetworkResponseReceivedEventArgs e)
        {
            if (!this.IsReady)
            {
                return;
            }
            else if (this.FirstResponse != null)
            {
                return;
            }
            else if (e.ResponseUrl.StartsWith("https://www.tiktok.com/api/post/item_list/"))
            {
                try
                {
                    this.FirstResponse = e;
                    this.ResetEvent.Set();
                }
                catch (Exception ex)
                {
                    this.Exception = ex;
                    this.ResetEvent.Set();
                }

            }

        }

    }

}
