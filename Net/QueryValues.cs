using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace SNSDownloader.Net
{
    public class QueryValues : List<QueryValue>
    {
        public static bool TryParse(string query, out QueryValues value)
        {
            try
            {
                value = Parse(query);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }

        }

        public static QueryValues Parse(string query)
        {
            if (query.StartsWith(HttpUtility2.QuerySeparator))
            {
                query = query[1..];
            }

            var queryValues = new QueryValues();
            var keyDelimiter = HttpUtility2.QueryKeyDelimiter;
            var splits = query.Split(HttpUtility2.QueryValuesDelimiter, StringSplitOptions.RemoveEmptyEntries);

            foreach (var pair in splits)
            {
                var delimiterIndex = pair.IndexOf(keyDelimiter);

                if (delimiterIndex == -1)
                {
                    queryValues.Add(new QueryValue(HttpUtility.UrlDecode(pair)));
                }
                else
                {
                    var key = HttpUtility.UrlDecode(pair[0..delimiterIndex]);
                    var value = HttpUtility.UrlDecode(pair[(delimiterIndex + keyDelimiter.Length)..]);
                    queryValues.Add(new QueryValue(key, value));
                }

            }

            return queryValues;
        }

        public QueryValues()
        {

        }

        public override string ToString() => $"{HttpUtility2.QuerySeparator}{string.Join(HttpUtility2.QueryValuesDelimiter, this)}";

        public void Add(string key) => this.Add(new QueryValue(key));

        public void Add<T>(string key, T value) => this.Add(new QueryValue(key, string.Concat(value)));

        public void AddRange<T>(string key, IEnumerable<T> values)
        {
            foreach (var value in values)
            {
                this.Add(key, value);
            }

        }

        public void RemoveAll(string key)
        {
            foreach (var value in this.ToArray())
            {
                if (value.Key.Equals(key, StringComparison.OrdinalIgnoreCase) == true)
                {
                    this.Remove(value);
                }

            }

        }

        public QueryValue Find(string key) => this.FindAll(key).FirstOrDefault();

        public IEnumerable<QueryValue> FindAll(string key) => this.Where(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

}