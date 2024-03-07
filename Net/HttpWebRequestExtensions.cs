using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using OpenQA.Selenium;

namespace SNSDownloader.Net
{
    public static class HttpWebRequestExtensions
    {
        public static WrappedHttpResponse GetWrappedResponse(this HttpWebRequest request)
        {
            try
            {
                return new WrappedHttpResponse(request, request.GetResponse() as HttpWebResponse, true);
            }
            catch (WebException e)
            {
                return new WrappedHttpResponse(request, e.Response as HttpWebResponse, false);
            }

        }

        public static void Bind(this HttpWebRequest request, NetworkRequestSentEventArgs e)
        {
            request.Method = e.RequestMethod;

            foreach (var pair in e.RequestHeaders)
            {
                request.Headers[pair.Key] = pair.Value;
            }

        }

    }

}
