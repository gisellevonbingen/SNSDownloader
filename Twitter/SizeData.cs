using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Twitter
{
    public class SizeData
    {
        public int Width { get; set; } = 0;
        public int Height { get; set; } = 0;
        public string Resize { get; set; } = string.Empty;

        public Size Size
        {
            get => new Size(this.Width, this.Height);
            set => (this.Width, this.Height) = (value.Width, value.Height);
        }

        public SizeData()
        {

        }

        public SizeData(JToken json) : this()
        {
            this.Width = json.Value<int>("w");
            this.Height = json.Value<int>("h");
            this.Resize = json.Value<string>("resize");
        }

        public JObject ToJson() => new JObject()
        {
            ["w"] = this.Width,
            ["h"] = this.Height,
            ["resize"] = this.Resize,
        };

        public override string ToString() => $"{this.Width}x{this.Height},{this.Resize}";

    }

}
