using System;
using System.Collections.Generic;
using System.Text;

namespace RipcordSoftware.HttpWebServer
{
    public class HttpWebRequestHeaders
    {
        private readonly SortedDictionary<string, string> headers = null;
        private long? contentLength = null;

        private HttpWebRequestHeaders(HttpWebBuffer buffer)
        {
            var headerBlock = Encoding.ASCII.GetString(buffer.Buffer, 0, buffer.DataLength);
            var headerLines = headerBlock.Split(new char[] { '\r', '\n' });

            headers = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (headerLines.Length > 0)
            {
                var firstLine = headerLines[0];

                var methodLength = firstLine.IndexOf(" ");
                if (methodLength < 3)
                {
                    throw new HttpWebServerException("Invalid header method");
                }

                var urlLength = firstLine.IndexOf(" ", methodLength + 1) - methodLength - 1;    
                if (urlLength < 1)
                {
                    throw new HttpWebServerException("Invalid header URL");
                }

                HttpMethod = firstLine.Substring(0, methodLength);
                RawUrl = firstLine.Substring(methodLength + 1, urlLength);
                Uri = RawUrl;
                Version = firstLine.Substring(methodLength + 1 + urlLength + 1);

                if (Version.Length < 5 || !Version.StartsWith("HTTP/"))
                {
                    throw new HttpWebServerException("Invalid header version");
                }

                Version = Version.Substring(5);

                for (int i = 1; i < headerLines.Length; i++)
                {
                    var line = headerLines[i];

                    var seperatorIndex = line.IndexOf(": ");
                    if (seperatorIndex > 0)
                    {
                        var header = line.Substring(0, seperatorIndex);
                        var value = line.Substring(seperatorIndex + 2).Trim();
                        headers.Add(header, value);
                    }
                }

                var queryStringIndex = Uri.IndexOf('?');
                if (queryStringIndex > 0)
                {
                    // get the query string
                    if ((queryStringIndex + 1) < Uri.Length)
                    {
                        QueryString = Uri.Substring(queryStringIndex + 1);
                    }

                    // remove the query string from the uri
                    Uri = Uri.Substring(0, queryStringIndex);
                }
            }
        }

        public static bool GetHeaders(HttpWebBuffer buffer, out HttpWebRequestHeaders headers, out HttpWebBuffer bodyBuffer)
        {            
            headers = null;
            bodyBuffer = null;

            int headerEnd = -1;
            for (int i = 3; i < buffer.DataLength; i++)
            {
                if (buffer[i] == '\n' && buffer[i - 1] == '\r' && buffer[i - 2] == '\n' && buffer[i - 3] == '\r')
                {
                    headerEnd = i;
                    break;
                }
            }

            if (headerEnd > 0)
            {
                headerEnd -= 3;
                int bodyStart = headerEnd + 4;

                var headerBuffer = new HttpWebBuffer(buffer.Buffer, 0, headerEnd);
                headers = new HttpWebRequestHeaders(headerBuffer);

                if (bodyStart < buffer.DataLength)
                {
                    bodyBuffer = new HttpWebBuffer(buffer.Buffer, bodyStart, buffer.DataLength - bodyStart);
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        public string HttpMethod { get; protected set; }
        public string Version { get; protected set; }

        public string RawUrl { get; protected set; }
        public string Uri { get; protected set; }
        public string QueryString { get; protected set; }

        public string ContentType { get { return this["Content-Type"]; } }
        public string AcceptEncoding { get { return this["Accept-Encoding"]; } }
        public string Accept { get { return this["Accept"]; } }
        public string Host { get { return this["Host"]; } }
        public string TransferEncoding { get { return this["Transfer-Encoding"]; } }
        public string ContentEncoding { get { return this["Content-Encoding"]; } }
        public string Expect { get { return this["Expect"]; } }
        public string UserAgent { get { return this["User-Agent"]; } }
        public string Connection { get { return this["Connection"]; } }

        public long? ContentLength 
        { 
            get 
            { 
                if (!contentLength.HasValue)
                {
                    var length = this["Content-Length"];
                    contentLength = length != null ? long.Parse(length) : (long?)null; 
                }

                return contentLength;
            } 
        }

        public string this[string header] { get { string value = null; headers.TryGetValue(header, out value); return value; } }

        public bool HasContentLength { get { return ContentLength != null; } }
        public bool IsChunkEncoded { get { var encoding = TransferEncoding; return encoding != null && encoding.Contains("chunked"); } }

        public bool IsGZipEncoded { get { var encoding = ContentEncoding; return encoding != null ? encoding.Contains("gzip") : false; } }
        public bool IsDeflateEncoded { get { var encoding = ContentEncoding; return encoding != null ? encoding.Contains("deflate") : false; } }
        public bool IsCompressed { get { return IsGZipEncoded || IsDeflateEncoded; } }
    }
}

