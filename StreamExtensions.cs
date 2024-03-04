using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SNSDownloader
{
    public static class StreamExtensions
    {
        public static byte[] ToArray(this Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

    }

}
