using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SNSDownloader.Configs
{
    public static class JsonUtils
    {
        public static JArray WriteCookies(IEnumerable<OpenQA.Selenium.Cookie> cookies)
        {
            return new JArray(cookies.Select(WriteCookie));
        }

        public static JObject WriteCookie(OpenQA.Selenium.Cookie cookie) => new JObject()
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

        public static IEnumerable<OpenQA.Selenium.Cookie> ReadCookies(JArray json)
        {
            if (json != null)
            {
                foreach (var element in json)
                {
                    yield return ReadCookie(element);
                }

            }

        }

        public static OpenQA.Selenium.Cookie ReadCookie(JToken element) => new OpenQA.Selenium.Cookie(
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
