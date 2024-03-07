using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SNSDownloader.Net;

namespace SNSDownloader.Twitter
{
    public class SearchTimelinePayload
    {
        public JObject Variables { get; set; } = new JObject();
        public JObject Features { get; set; } = new JObject();

        public SearchTimelinePayload()
        {

        }

        public SearchTimelinePayload(QueryValues queries)
        {
            this.Variables = JObject.Parse(queries.Find("variables").Value);
            this.Features = JObject.Parse(queries.Find("features").Value);
        }

        public IEnumerable<QueryValue> ToValues()
        {
            yield return new QueryValue("variables", this.Variables.ToString(Formatting.None));
            yield return new QueryValue("features", this.Features.ToString(Formatting.None));
        }

        public override string ToString() => $"Variables={this.Variables}, Features={this.Features}";

    }

}
