﻿using System;
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
            this.Cookies = JsonUtils.ReadCookies(json.Value<JArray>("Cookies")).ToList();
        }

        public void Save(JToken json)
        {
            json["Cookies"] = JsonUtils.WriteCookies(this.Cookies);
        }

    }

}
