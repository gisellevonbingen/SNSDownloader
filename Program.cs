using System;
using System.Collections.Generic;
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
        public static Regex TweetStatusPattern { get; } = new Regex("https:\\/\\/twitter\\.com\\/(?<user_id>.+)\\/status\\/(?<tweet_id>.+)");
        public static Regex TweetDetailPattern { get; } = new Regex("https:\\/\\/twitter\\.com\\/i\\/api\\/graphql\\/\\w+\\/TweetDetail");
        public static Regex VidPattern { get; } = new Regex(".+/vid\\/(?<size>\\w+)\\/.*");

        public static void Main()
        {
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
                else if (TweetStatusPattern.IsMatch(url) == false)
                {
                    Console.WriteLine("Wrong tweet url");
                    continue;
                }

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

                    for (var i = 0; i < mediaList.Count; i++)
                    {
                        var media = mediaList[i];
                        var variants = media.VideoInfo.Variants;
                        Console.WriteLine($"==================== Media {i + 1}/{mediaList.Count}");
                        Console.WriteLine($"Thumbnail: {media.Thumbnail}");
                        Console.WriteLine($"Duration : {media.VideoInfo.Duration} ms");
                        Console.WriteLine($"Variants Found : {variants.Count}");
                        Console.WriteLine();

                        for (int j = 0; j < variants.Count; j++)
                        {
                            var variant = variants[j];
                            Console.WriteLine($"========== Varient {j + 1}/{variants.Count}");
                            Console.WriteLine($"Url: {variant.Url}");

                            if (variant.ContentType.Equals("video/mp4") == true)
                            {
                                var size = VidPattern.Match(variant.Url).Groups["size"];
                                Console.WriteLine($"Size: {size.Value}");
                            }
                            else if (variant.ContentType.Equals("application/x-mpegURL") == true)
                            {
                                var http = WebRequest.CreateHttp(new Uri(variant.Url));
                                http.Method = HttpMethod.Get.Method;

                                using var response = http.GetResponse();
                                using var responseReader = new StreamReader(response.GetResponseStream());
                                var m3u8 = responseReader.ReadToEnd();
                                Console.WriteLine();
                                Console.WriteLine("===== start of m3u8");
                                Console.WriteLine(m3u8);
                                Console.WriteLine("===== end of m3u8");
                            }
                            else
                            {
                                Console.WriteLine($"Unknown ContentType: {variant.ContentType}");
                            }

                            Console.WriteLine();
                        }

                    }

                }

            }

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
