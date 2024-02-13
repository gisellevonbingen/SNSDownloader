using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TwitterVideoDownloader
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
