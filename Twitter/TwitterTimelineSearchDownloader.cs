﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using SNSDownloader.Net;
using SNSDownloader.Util;

namespace SNSDownloader.Twitter
{
    public class TwitterTimelineSearchDownloader : AbstractDownloader
    {
        public const string Prefix = "TwitterTimelineSearch:";
        public static Regex SearchTimelinePattern { get; } = TwitterUtils.GetGraphqlPattern("SearchTimeline");

        private NetworkRequestSentEventArgs FirstRequest;
        private string RawQuery;
        private readonly AutoResetEvent ResetEvent;

        public TwitterTimelineSearchDownloader()
        {
            this.ResetEvent = new AutoResetEvent(false);
        }

        public override string PlatformName => "TwitterTimelineSearch";

        public override void OnNetworkCreated(INetwork network)
        {
            network.NetworkRequestSent += this.OnNetworkRequestSent;
        }

        protected override void OnReset()
        {
            this.FirstRequest = null;
            this.RawQuery = null;
            this.ResetEvent.Reset();
        }

        public override bool Test(string url) => url.StartsWith(Prefix);

        public override string GetRequestUrl() => "https://x.com/search?q=" + HttpUtility.UrlEncode(this.RawQuery) + "&src=typed_query&f=live";

        protected override bool OnReady(string url)
        {
            this.RawQuery = url[Prefix.Length..];
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
            else if (this.FirstRequest == null)
            {
                this.Log("Not found");
                return DownloadResult.Failed;
            }
            else
            {
                this.Log($"Found");

                var path = Path.Combine(output.Directory, $"{string.Concat(this.RawQuery.Split(Path.GetInvalidFileNameChars()))}_{DateTime.Now.ToFileNameString()}.json");
                FileStream fs = null;
                StreamWriter sw = null;
                JsonTextWriter jtw = null;


                var nextUri = new Uri(this.FirstRequest.RequestUrl);
                var totalCount = 0;
                var t = 0;

                while (nextUri != null)
                {
                    var request = Program.CreateRequest(nextUri);
                    request.Bind(this.FirstRequest);

                    var rAsString = string.Empty;
                    var hasException = false;

                    try
                    {
                        using var response = request.GetWrappedResponse();
                        rAsString = response.Response.ReadAsString(Program.UTF8WithoutBOM);
                        var body = JObject.Parse(rAsString);

                        var (tweets, cursor) = this.GetTweetsAndNextCursor(body);

                        if (tweets.Count > 0)
                        {
                            if (tweets.Select(TwitterUtils.GetStatusUrl).All(output.Progressed.Contains))
                            {
                                if (t >= 2)
                                {
                                    this.Log($"All progressed");
                                    break;
                                }
                                else
                                {
                                    t++;
                                }

                            }

                        }
                        else if (t >= 2)
                        {
                            break;
                        }
                        else
                        {
                            t++;
                        }

                        var added = 0;

                        foreach (var tweet in tweets)
                        {
                            var url = TwitterUtils.GetStatusUrl(tweet);

                            if (!output.Progressed.Contains(url))
                            {
                                if (jtw == null)
                                {
                                    fs = new FileStream(path, FileMode.Create);
                                    sw = new StreamWriter(fs, Program.UTF8WithoutBOM);
                                    jtw = new JsonTextWriter(sw) { Formatting = Formatting.Indented };
                                    jtw.WriteStartArray();
                                }

                                jtw.WriteValue(url);
                                added += 1;
                                t = 0;
                            }

                        }

                        if (jtw != null)
                        {
                            jtw.Flush();
                        }

                        this.Log("========== Cursor ==========");

                        if (tweets.Count > 1)
                        {
                            this.Log($"Period: {tweets[0].CreatedAt.ToStandardString()} ~ {tweets[^1].CreatedAt.ToStandardString()}");
                        }

                        totalCount += added;
                        this.Log($"Count: {totalCount}(+{added})");

                        if (!string.IsNullOrEmpty(cursor))
                        {
                            var queries = QueryValues.Parse(nextUri.Query);
                            var payload = new SearchTimelinePayload(queries);
                            payload.Variables["cursor"] = cursor;

                            foreach (var query in payload.ToValues())
                            {
                                queries.RemoveAll(query.Key);
                                queries.Add(query);
                            }

                            var builder = new UriBuilder(nextUri) { Query = queries.ToString() };
                            nextUri = builder.Uri;
                        }
                        else
                        {
                            nextUri = null;
                        }

                        Thread.Sleep(Math.Max(tweets.Count, 10) * 1000);
                    }
                    catch (Exception e)
                    {
                        this.Log("========== Exception ==========");
                        this.Log($"URI: {nextUri}");
                        this.Log($"Response: {rAsString}");
                        this.Log(e);
                        hasException = true;
                    }

                    if (hasException)
                    {
                        Thread.Sleep(60 * 1000);
                    }

                }

                if (jtw != null)
                {
                    jtw.WriteEndArray();
                }

                jtw.DisposeQuietly();
                sw.DisposeQuietly();
                fs.DisposeQuietly();

                if (totalCount == 0)
                {
                    File.Delete(path);
                }

                return DownloadResult.Success;
            }

        }

        public override bool CanSkip => false;

        private (List<TweetResultTweet> Tweets, string Cursor) GetTweetsAndNextCursor(JObject body)
        {
            var instructions = body.SelectToken("data.search_by_raw_query.search_timeline.timeline.instructions");

            var entires = TwitterUtils.GetTimelineEntries(instructions);
            var tweets = new List<TweetResultTweet>();
            var bottom = string.Empty;

            foreach (var entry in entires)
            {
                if (entry.Content is TimelineEntryContentItem item)
                {
                    if (item.Result is TweetResultTweet tweet)
                    {
                        tweets.Add(tweet);
                    }

                }
                else if (entry.Content is TimelineEntryContentCursor cursor)
                {
                    if (string.Equals(cursor.CursorType, "Bottom"))
                    {
                        bottom = cursor.Value;
                    }

                }

            }

            return (tweets, bottom);
        }

        private void OnNetworkRequestSent(object sender, NetworkRequestSentEventArgs e)
        {
            if (!this.IsReady)
            {
                return;
            }
            else if (this.FirstRequest != null)
            {
                return;
            }
            else if (SearchTimelinePattern.IsMatch(e.RequestUrl) && QueryValues.TryParse(new Uri(e.RequestUrl).Query, out var queries))
            {
                try
                {
                    var payload = new SearchTimelinePayload(queries);
                    var rawQuery = payload.Variables.Value<string>("rawQuery");

                    if (string.Equals(this.RawQuery, rawQuery))
                    {
                        this.FirstRequest = e;
                        this.ResetEvent.Set();
                    }

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
