using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using OpenQA.Selenium;

namespace SNSDownloader.Twitter
{
    public class TwitterSpaceDownloader : AbstractDownloader
    {
        public static Regex AudioSpacePattern { get; } = TwitterUtils.GetGraphqlPattern("AudioSpaceById");

        public TwitterSpaceDownloader()
        {

        }

        public override string PlatformName => "TwitterSpace";

        public override void OnNetworkCreated(INetwork network) => throw new NotImplementedException();

        protected override void OnReset() => throw new NotImplementedException();

        public override bool Test(string url) => throw new NotImplementedException();

        protected override bool OnReady(string url) => throw new NotImplementedException();

        public override bool Download(DownloadOutput output) => throw new NotImplementedException();

    }

}
