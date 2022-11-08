using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace TwitterVideoDownloader
{
    public class Program
    {
        public static string TweetIdGroup { get; } = "tweet_id";
        public static Regex TweetStatusPattern { get; } = new Regex($"https:\\/\\/twitter\\.com\\/(?<user_id>.+)\\/status\\/(?<{TweetIdGroup}>.+)");
        public static Regex TweetDetailPattern { get; } = new Regex("https:\\/\\/twitter\\.com\\/i\\/api\\/graphql\\/\\w+\\/TweetDetail");
        public static Regex VidPattern { get; } = new Regex(".+/vid\\/(?<size>\\w+)\\/.*");
        public static Regex PlPattern { get; } = new Regex(".+/pl\\/(?<size>\\w+)\\/.*");
        public static string MapUriPrefix { get; } = "#EXT-X-MAP:URI";
        public static string OutputDirectory { get; } = "./Output";
        public static int DownloadBufferSize { get; } = 16 * 1024;

        public static void Main()
        {
            var outputDirectory = Directory.CreateDirectory(OutputDirectory);
            outputDirectory.Create();

            var mediaList = new List<TwitterExtendedMediaEntity>();
            using var are = new AutoResetEvent(false);

            var driverService = ChromeDriverService.CreateDefaultService(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            driverService.HideCommandPromptWindow = true;
            driverService.SuppressInitialDiagnosticInformation = true;

            var options = new ChromeOptions();
            options.AddArgument("--headless");
            options.AddArgument("--silent");
            Console.WriteLine("Driver starting");
            using var driver = new ChromeDriver(driverService, options);
            var network = driver.Manage().Network;
            network.StartMonitoring();
            network.NetworkResponseReceived += (sender, e) => OnNetworkResponseReceived(e, are, mediaList);

            while (true)
            {
                Console.WriteLine("Enter tweet url");
                Console.Write("> ");
                var url = Console.ReadLine();

                if (url == null || url.Equals(":exit", StringComparison.OrdinalIgnoreCase) == true)
                {
                    break;
                }

                var statusMatch = TweetStatusPattern.Match(url);

                if (statusMatch.Success == false)
                {
                    Console.WriteLine("Wrong tweet url");
                    continue;
                }

                var tweetId = statusMatch.Groups[TweetIdGroup].Value;

                mediaList.Clear();
                driver.Navigate().GoToUrl(url);
                are.WaitOne();

                if (mediaList.Count == 0)
                {
                    Console.WriteLine("Media not found");
                }
                else
                {
                    Console.WriteLine($"Media Found : {mediaList.Count}");
                    Console.WriteLine();

                    var downladIndex = 0;

                    for (var i = 0; i < mediaList.Count; i++)
                    {
                        var media = mediaList[i];
                        var variants = media.VideoInfo.Variants;
                        var downloadList = variants.SelectMany(v => GetDownloadDataList(v)).ToArray();

                        Console.WriteLine($"{new string('=', 20)} Media {i + 1}/{mediaList.Count}");
                        Console.WriteLine($"Thumbnail: {media.Thumbnail}");
                        Console.WriteLine($"Duration : {media.VideoInfo.Duration} ms");
                        Console.WriteLine($"Variants Found : {variants.Count}");
                        Console.WriteLine($"Downloads Found : {downloadList.Length}");
                        Console.WriteLine();

                        for (var k = 0; k < downloadList.Length; k++)
                        {
                            var download = downloadList[k];

                            if (download.Segments.Count == 0)
                            {
                                continue;
                            }

                            var fileName = $"{tweetId}_{++downladIndex}_{download.Size.Width}x{download.Size.Height}_{Path.GetFileName(download.Segments[0].LocalPath)}";

                            Console.WriteLine($"{new string('=', 10)} File {k + 1}/{downloadList.Length}");
                            Console.WriteLine($"Size : {download.Size}");
                            Console.WriteLine($"File Name : {fileName}");
                            Console.WriteLine();

                            using var fs = new FileStream(Path.Combine(outputDirectory.FullName, fileName), FileMode.Create);

                            foreach (var segment in download.Segments)
                            {
                                using var segmentResponse = WebRequest.CreateHttp(segment).GetResponse();
                                using var segmentResponseStream = segmentResponse.GetResponseStream();
                                segmentResponseStream.CopyTo(fs, DownloadBufferSize);
                            }

                        }

                        Console.WriteLine();
                    }

                }

            }

        }

        private static IEnumerable<DownloadData> GetDownloadDataList(TwitterVideoVariant variant)
        {
            if (variant.ContentType.Equals("video/mp4") == true)
            {
                var size = ParseSize(VidPattern.Match(variant.Url).Groups["size"].Value);
                yield return new DownloadData(new Uri(variant.Url)) { Size = size };
            }
            else if (variant.ContentType.Equals("application/x-mpegURL") == true)
            {
                var variantUri = new Uri(variant.Url);
                using var response = WebRequest.CreateHttp(variantUri).GetResponse();
                using var responseReader = new StreamReader(response.GetResponseStream());

                for (string line = null; (line = responseReader.ReadLine()) != null;)
                {
                    if (line.StartsWith("#") == true)
                    {
                        continue;
                    }

                    var size = ParseSize(PlPattern.Match(line).Groups["size"].Value);
                    var m3u8Uri = new Uri(variantUri, line);
                    var segments = GetM3U8Segments(m3u8Uri).Select(s => new Uri(m3u8Uri, s));
                    yield return new DownloadData(segments) { Size = size };
                }

            }
            else
            {
                Console.WriteLine($"Unknown ContentType: {variant.ContentType}");
            }

        }

        private static IEnumerable<string> GetM3U8Segments(Uri uri)
        {
            using var response = WebRequest.CreateHttp(uri).GetResponse();
            using var responseReader = new StreamReader(response.GetResponseStream());

            for (string line = null; (line = responseReader.ReadLine()) != null;)
            {
                if (line.StartsWith(MapUriPrefix) == true)
                {
                    //#EXT-X-MAP:URI=
                    var mapUri = line[(MapUriPrefix.Length + 1)..];

                    if (mapUri.StartsWith("\"") == true && mapUri.EndsWith("\"") == true)
                    {
                        yield return mapUri[1..^1];
                    }
                    else
                    {
                        yield return mapUri;
                    }

                }
                else if (line.StartsWith("#") == true)
                {
                    continue;
                }
                else
                {
                    yield return line;
                }

            }

        }

        private static Size ParseSize(string text)
        {
            var splits = text.Split('x');
            var width = int.Parse(splits[0]);
            var height = int.Parse(splits[1]);
            return new Size(width, height);
        }

        private static void OnNetworkResponseReceived(NetworkResponseReceivedEventArgs e, AutoResetEvent are, List<TwitterExtendedMediaEntity> list)
        {
            if (TweetDetailPattern.IsMatch(e.ResponseUrl) == false)
            {
                return;
            }

            try
            {
                var body = JObject.Parse(e.ResponseBody);
                var instructions = body.SelectToken("data.threaded_conversation_with_injections_v2.instructions");

                if (instructions == null)
                {
                    return;
                }

                foreach (var instruction in instructions)
                {
                    var type = instruction.Value<string>("type");

                    if (string.Equals(type, "TimelineAddEntries") == false)
                    {
                        continue;
                    }

                    foreach (var entry in instruction.Value<JArray>("entries"))
                    {
                        var content = entry.Value<JObject>("content");
                        var entryType = content?.Value<string>("entryType");
                        var itemContent = content?.Value<JObject>("itemContent");
                        var itemType = itemContent?.Value<string>("itemType");

                        if (string.Equals(entryType, "TimelineTimelineItem") == true && string.Equals(itemType, "TimelineTweet") == true)
                        {
                            var extended_entities = itemContent.SelectToken("tweet_results.result.legacy.extended_entities");

                            if (extended_entities == null)
                            {
                                continue;
                            }

                            var mediaArray = extended_entities.Value<JArray>("media").ToArray();
                            list.AddRange(mediaArray.Select(t => new TwitterExtendedMediaEntity(t)));
                        }

                    }

                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                are.Set();
            }

        }

    }

}
