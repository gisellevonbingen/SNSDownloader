using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace SNSDownloader.M3U8
{
    public static class M3U8Utils
    {
        public const string MapUriPrefix  = "#EXT-X-MAP:URI";

        public static IEnumerable<string> GetSegments(StreamReader input)
        {
            for (string line = null; (line = input.ReadLine()) != null;)
            {
                if (line.StartsWith(MapUriPrefix) == true)
                {
                    //#EXT-X-MAP:URI=
                    var mapUri = line[(MapUriPrefix.Length + 1)..];

                    if (mapUri.StartsWith("\"") == true && mapUri.EndsWith("\"") == true)
                    {
                        yield return mapUri[1..^1];
                    }
                    else
                    {
                        yield return mapUri;
                    }

                }
                else if (line.StartsWith("#") == true)
                {
                    continue;
                }
                else
                {
                    yield return line;
                }

            }

        }

    }

}
