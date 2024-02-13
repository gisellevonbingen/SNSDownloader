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
            var responseStream = response.GetResponseStream();

            if (contentEncoding.Equals("gzip") == true)
            {
                return new GZipStream(responseStream, CompressionMode.Decompress);
            }
            else
            {
                throw new IndexOutOfRangeException($"Unknown ContentEncoding: {contentEncoding}");
            }

        }

    }

}
