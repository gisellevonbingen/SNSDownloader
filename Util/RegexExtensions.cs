using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SNSDownloader.Util
{
    public static class RegexExtensions
    {
        public static bool TryMatch(this Regex regex, string input, out Match match)
        {
            match = regex.Match(input);
            return match.Success;
        }

    }

}
