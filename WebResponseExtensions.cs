using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace TwitterVideoDownloader
{
    public static class WebResponseExtensions
    {
        public static Stream GetDecompressedResponseStream(this WebResponse response)
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
