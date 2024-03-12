using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Configs
{
    public class TwitterConfig
    {
        public List<OpenQA.Selenium.Cookie> Cookies { get; }

        public TwitterConfig(JToken json)
        {
            this.Cookies = new List<OpenQA.Selenium.Cookie>((json.Value<JArray>("Cookies") ?? new JArray()).Select(ToCookie));
        }

        public void Save(JToken json)
        {
            json["Cookies"] = new JArray(this.Cookies.Select(ToJson));
        }

        public static JObject ToJson(OpenQA.Selenium.Cookie cookie) => new JObject()
        {
            ["Name"] = cookie.Name,
            ["Value"] = cookie.Value,
            ["Domain"] = cookie.Domain,
            ["Path"] = cookie.Path,
            ["Expiry"] = cookie.Expiry,
            ["Secure"] = cookie.Secure,
            ["IsHttpOnly"] = cookie.IsHttpOnly,
            ["SameSite"] = cookie.SameSite,
        };

        public static OpenQA.Selenium.Cookie ToCookie(JToken element) => new OpenQA.Selenium.Cookie(
            element.Value<string>("Name"),
            element.Value<string>("Value"),
            element.Value<string>("Domain"),
            element.Value<string>("Path"),
            element.Value<DateTime?>("Expiry"),
            element.Value<bool>("Secure"),
            element.Value<bool>("IsHttpOnly"),
            element.Value<string>("SameSite")
        );

    }

}
