using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace SNSDownloader
{
    public static class WebResponseExtensions
    {
        public static string ReadAsString(this WebResponse response, Encoding encoding)
        {
            using var reader = response.ReadAsReader(encoding);
            return reader.ReadToEnd();
        }

        public static StreamReader ReadAsReader(this WebResponse response, Encoding encoding)
        {
            var stream = ReadAsStream(response);
            return new StreamReader(stream, encoding);
        }

        public static Stream ReadAsStream(this WebResponse response)
        {
            var contentEncoding = response.Headers[HttpResponseHeader.ContentEncoding];
            var original = response.GetResponseStream();

            if (string.IsNullOrEmpty(contentEncoding) == true)
            {
                return original;
            }
            else if (contentEncoding.Equals("gzip") == true)
            {
                return new GZipStream(original, CompressionMode.Decompress);
            }
            else
            {
                throw new IndexOutOfRangeException($"Unknown ContentEncoding: {contentEncoding}");
            }

        }

    }

}
