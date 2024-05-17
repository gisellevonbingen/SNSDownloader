using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public static class TwitterUtils
    {
        public static string TweetIdGroup { get; } = "tweet_id";
        public static IEnumerable<string> Domains { get; } = new string[] { "https://x.com", "https://twitter.com", };
        public static IEnumerable<Regex> StatusPatterns { get; } = Domains.Select(Regex.Escape).Select(s => new Regex($"{s}\\/(?<user_id>.+)\\/status\\/(?<{TweetIdGroup}>\\d+)(\\?.+)?"));

        public static Regex GetGraphqlPattern(string api) => new Regex($"https:\\/\\/x\\.com\\/i\\/api\\/graphql\\/.+\\/{api}");

        public static string GetStatusUrl(TweetResultTweet tweet) => GetStatusUrl(tweet.User.ScreenName, tweet.Id);

        public static string GetStatusUrl(string screenName, string tweetId) => $"https://x.com/{screenName}/status/{tweetId}";

        public static string GetLiveVideoStreamUrl(string mediaKey) => $"https://x.com/i/api/1.1/live_video_stream/status/{mediaKey}";

        public static string GetTweetId(string url)
        {
            foreach (var pattern in StatusPatterns)
            {
                var statusMatch = pattern.Match(url);

                if (statusMatch.Success == true)
                {
                    return statusMatch.Groups[TweetIdGroup].Value;
                }

            }

            return null;
        }

        public static IEnumerable<TimelineEntry> GetTimelineEntries(JToken instructions)
        {
            foreach (var instruction in instructions)
            {
                var instructionType = instruction.Value<string>("type");

                if (string.Equals(instructionType, "TimelineAddEntries"))
                {
                    foreach (var entry in instruction.Value<JArray>("entries"))
                    {
                        yield return new TimelineEntry(entry);
                    }

                }
                else if (string.Equals(instructionType, "TimelineReplaceEntry"))
                {
                    yield return new TimelineEntry(instruction.Value<JToken>("entry"));
                }

            }

        }

    }

}
