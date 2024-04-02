using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using SNSDownloader.Net;

namespace SNSDownloader.Twitter
{
    public class TwitterAudioSpaceDownloader : AbstractDownloader
    {
        public static Regex RequestUriPattern { get; } = new Regex("https:\\/\\/twitter.com\\/i\\/spaces/(?<id>.+)");
        public static Regex AudioSpacePattern { get; } = TwitterUtils.GetGraphqlPattern("AudioSpaceById");

        private readonly AutoResetEvent ResultResetEvent;

        private KeyValuePair<string, string>[] RequestHeaders;
        private AudioSpaceResult Result;

        public TwitterAudioSpaceDownloader()
        {
            this.ResultResetEvent = new AutoResetEvent(false);

            this.RequestHeaders = null;
            this.Result = null;
        }

        public override string PlatformName => "TwitterSpace";

        public override void OnNetworkCreated(INetwork network)
        {
            network.NetworkRequestSent += this.OnNetworkRequestSent;
            network.NetworkResponseReceived += this.OnNetworkResponseReceived;
        }

        protected override void OnReset()
        {
            this.ResultResetEvent.Reset();

            this.RequestHeaders = null;
            this.Result = null;
        }

        public override bool Test(string url) => this.Test(url, out _);

        public bool Test(string url, out string id)
        {
            id = this.GetId(url);
            return !string.IsNullOrEmpty(id);
        }

        public string GetId(string url) => RequestUriPattern.Match(url).Groups["id"].Value;

        protected override bool OnReady(string url) => true;

        public override bool Download(DownloadOutput output)
        {
            if (!this.TryGetResult(out var result))
            {
                return false;
            }
            else if (result == null)
            {
                this.Log("Not found");
                return false;
            }
            else
            {
                this.Log($"Found");
                return false;
            }

        }

        public bool TryGetResult(out AudioSpaceResult result)
        {
            if (!this.WaitAll(this.ResultResetEvent))
            {
                result = null;
                return false;
            }
            else if (this.Exception != null)
            {
                throw new Exception(string.Empty, this.Exception);
            }
            else
            {
                result = this.Result;
                return true;
            }

        }

        private bool EqualsAudioSpaceUrl(string url)
        {
            if (AudioSpacePattern.IsMatch(url) == false)
            {
                return false;
            }
            else if (QueryValues.TryParse(new Uri(url).Query, out var queries) == false)
            {
                return false;
            }
            else
            {
                var jVariables = JObject.Parse(queries.Find("variables").Value);
                var responseId = jVariables.Value<string>("id");
                var requestId = this.GetId(this.Url);
                return string.Equals(requestId, responseId);
            }

        }

        private void OnNetworkRequestSent(object sender, NetworkRequestSentEventArgs e)
        {
            if (!this.IsReady)
            {
                return;
            }
            else if (this.EqualsAudioSpaceUrl(e.RequestUrl))
            {
                this.RequestHeaders = e.RequestHeaders.ToArray();
            }

        }

        private void OnNetworkResponseReceived(object sender, NetworkResponseReceivedEventArgs e)
        {
            if (!this.IsReady)
            {
                return;
            }
            else if (this.EqualsAudioSpaceUrl(e.ResponseUrl))
            {
                try
                {
                    var body = JObject.Parse(e.ResponseBody);
                    var metadata = body.SelectToken("data.audioSpace.metadata");
                    var audioSpace = new AudioSpace(metadata);

                    var media = this.GetLiveVideoStream(audioSpace, this.RequestHeaders);
                    this.Result = new AudioSpaceResult()
                    {
                        AudioSpace = audioSpace,
                        SourceLocation = media.SelectToken("source.location").Value<string>(),
                    };

                }
                catch (Exception ex)
                {
                    this.Exception = ex;
                }

                this.ResultResetEvent.Set();
            }

        }

        private JObject GetLiveVideoStream(AudioSpace audioSpace, KeyValuePair<string, string>[] requestHeaders)
        {
            var request = Program.CreateRequest(TwitterUtils.GetLiveVideoStreamUrl(audioSpace.MediaKey));
            Program.PutHeaders(request, requestHeaders);
            var response = request.GetWrappedResponse();

            var text = response.Response.ReadAsString(Program.UTF8WithoutBOM);
            return JObject.Parse(text);
        }

    }

}
