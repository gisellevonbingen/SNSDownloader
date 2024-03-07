using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace SNSDownloader.Net
{
    public struct QueryValue
    {
        public string Key { get; set; }
        public bool HasValue { get; set; }
        public string Value { get; set; }

        public QueryValue(string key)
        {
            this.Key = key;
            this.HasValue = false;
            this.Value = string.Empty;
        }

        public QueryValue(string key, string value) : this()
        {
            this.Key = key;
            this.HasValue = true;
            this.Value = value;
        }

        public override string ToString()
        {
            if (this.HasValue)
            {
                return $"{HttpUtility.UrlEncode(this.Key)}{HttpUtility2.QueryKeyDelimiter}{HttpUtility.UrlEncode(this.Value)}";
            }
            else
            {
                return HttpUtility.UrlEncode(this.Key);
            }

        }

    }

}
