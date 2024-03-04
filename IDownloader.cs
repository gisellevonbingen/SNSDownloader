using System;
using System.Collections.Generic;
using System.Text;
using OpenQA.Selenium;

namespace SNSDownloader
{
    public interface IDownloader
    {
        string PlatformName { get; }

        void OnNetworkCreated(INetwork network);

        void Reset();

        bool Test(string url);

        bool Download(string url, string outputDirectory);
    }

}
