using System;
using System.Collections.Generic;
using System.Text;

namespace SNSDownloader.Util
{
    public static class DateTimeExtensions
    {
        public static string ToStandardString(this DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss");

        public static string ToFileNameString(this DateTime value) => value.ToString("yyyyMMdd_HHmmss");

        public static string ToYearMonthString(this DateTime value) => value.ToString("yyyy-MM");
    }

}
