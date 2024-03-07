using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public static class TwitterUtils
    {
        public static string TweetIdGroup { get; } = "tweet_id";
        public static Regex XStatusPattern { get; } = new Regex($"https:\\/\\/x\\.com\\/(?<user_id>.+)\\/status\\/(?<{TweetIdGroup}>\\d+)(\\?.+)?");
        public static Regex TweetStatusPattern { get; } = new Regex($"https:\\/\\/twitter\\.com\\/(?<user_id>.+)\\/status\\/(?<{TweetIdGroup}>\\d+)(\\?.+)?");
        public static IEnumerable<Regex> TweetStatusPatterns { get; } = new[] { XStatusPattern, TweetStatusPattern };

        public static string GetStatusUrl(TimelineTweet tweet) => GetStatusUrl(tweet.User.ScreenName, tweet.Id);

        public static string GetStatusUrl(string screenName, string tweetId) => $"https://twitter.com/{screenName}/status/{tweetId}";

        public static string GetTweetId(string url)
        {
            foreach (var pattern in TweetStatusPatterns)
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
