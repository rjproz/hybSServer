using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.IO;

using Ionic.Zlib;

namespace RipcordSoftware.HttpWebServer
{
    public class HttpWebResponse
    {
        #region Types
        public class ResponseHeaders : IEnumerable<KeyValuePair<string, string>>
        {
            #region Private fields
            private SortedDictionary<string, string> headers = new SortedDictionary<string, string>();
            #endregion

            #region Constructors
            public ResponseHeaders()
            {
            }
            #endregion

            #region Public properties
            public string this[string header]
            {
                get
                {
                    string value = null;
                    headers.TryGetValue(header, out value);
                    return value;
                }

                set
                {
                    if (value != null)
                    {
                        headers[header] = value;
                    }
                    else
                    {
                        headers.Remove(header);
                    }
                }
            }

            public string[] Keys
            { 
                get
                { 
                    var keys = new string[headers.Count];
                    headers.Keys.CopyTo(keys, 0);                    
                    return keys;
                }
            }    
            #endregion

            #region IEnumerable implementation

            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            {
                return headers.GetEnumerator();
            }

            #endregion

            #region IEnumerable implementation

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return headers.GetEnumerator();
            }

            #endregion
        }

        
        #endregion

        #region Private fields
        private readonly HttpWebSocket socket;
        private readonly int keepAliveTimeout;
        private Stream stream;

        private string version;

        private readonly HttpWebStringBuilderPool headerStringPool = new HttpWebStringBuilderPool(256, 4096);
        #endregion

        #region Constructor
        internal HttpWebResponse(HttpWebSocket socket, int keepAliveTimeout)
        {
            this.socket = socket;
            this.keepAliveTimeout = keepAliveTimeout;

            Headers = new ResponseHeaders();

            StatusCode = 200;
            Version = HttpWebServer.HttpVersion11;
        }
        #endregion

        #region Public properties
        public int StatusCode { get; set; }
        public string StatusDescription { get; set; }

        public string Version 
        { 
            get
            {
                return version;
            }

            set
            {            
                // remove the leading HTTP/ from the string
                if (value != null && value.StartsWith("HTTP/") && value.Length > 5)
                {
                    value = value.Substring(5);
                }

                switch (value)
                {
                    case HttpWebServer.HttpVersion10:
                        if (string.IsNullOrEmpty(Connection))
                        {
                            KeepAlive = false;
                        }
                        break;

                    case HttpWebServer.HttpVersion11:
                        if (string.IsNullOrEmpty(Connection))
                        {
                            KeepAlive = true;
                        }
                        break;

                    default:
                        throw new ArgumentException("Version");
                }

                version = value;
            }
        }

        public ResponseHeaders Headers { get; protected set; }

        public long? ContentLength 
        { 
            get
            { 
                var contentLength = Headers["Content-Length"]; 
                return contentLength != null ? long.Parse(contentLength) : (long?)null;
            }

            set 
            {
                Headers["Content-Length"] = value != null ? value.ToString() : null;
            }
        }

        public string ContentType { get { return Headers["Content-Type"]; } set { Headers["Content-Type"] = value; } }
        public string ContentEncoding { get { return Headers["Content-Encoding"]; } set { Headers["Content-Encoding"] = value; } }
        public string TransferEncoding { get { return Headers["Transfer-Encoding"]; } set { Headers["Transfer-Encoding"] = value; } }
        public string Connection { get { return Headers["Connection"]; } set { Headers["Connection"] = value; } }       

        public bool KeepAlive
        { 
            get 
            { 
                return string.Compare(Connection, "Keep-Alive", true) == 0;
            } 

            set 
            { 
                if (value) 
                { 
                    Connection = "Keep-Alive";
                } 
                else 
                { 
                    Connection = "close";
                } 
            } 
        }

        public bool IsChunked { get { var encoding = TransferEncoding; return encoding != null && encoding.Contains("chunked"); } }

        public bool IsResponseActive { get { return stream != null; } }
        #endregion

        #region Public methods
        public Stream GetResponseStream(string acceptEncoding)
        {
            if (stream != null)
            {
                throw new HttpWebServerResponseException("GetResponseStream() may not be called more than once");
            }

            // we support compression when we know the content type and client capabilities
            if (!string.IsNullOrEmpty(acceptEncoding) && !string.IsNullOrEmpty(ContentType))
            {
                // lookup the content type attributes
                var contentType = ContentType;
                var contentTypeInfo = HttpWebMimeTypes.LookupByContentType(contentType);

                // determine if the client can handle compression and if the content type is compressible
                if (contentTypeInfo != null && contentTypeInfo.Compressible && !string.IsNullOrEmpty(acceptEncoding) && string.IsNullOrEmpty(Headers["Content-Encoding"]))
                {
                    if (acceptEncoding.Contains("deflate"))
                    {
                        ContentEncoding = "deflate";
                    }
                    else if (acceptEncoding.Contains("gzip"))
                    {
                        ContentEncoding = "gzip";
                    }

                    // remove the content length and enable chunked encoding
                    ContentLength = null;
                    TransferEncoding = "chunked";
                }
            }

            if (KeepAlive)
            {
                if (!IsChunked && !ContentLength.HasValue)
                {
                    TransferEncoding = "chunked";
                }
            }

            var headers = GetHeaders();
                    
            stream = new HttpWebResponseStream(this.socket, headers, KeepAlive, IsChunked);

            if (!string.IsNullOrEmpty(ContentEncoding))
            {
                if (ContentEncoding.Contains("deflate"))
                {
                    stream = new DeflateStream(stream, CompressionMode.Compress, CompressionLevel.Default);
                }
                else if (ContentEncoding.Contains("gzip"))
                {
                    stream = new GZipStream(stream, CompressionMode.Compress, CompressionLevel.Default);
                }
            }

            return stream;
        }
            
        public void Close()
        {
            if (stream == null)
            {
                // there is no body, set the content length correctly
                if (KeepAlive)
                {
                    ContentLength = 0;
                }
                else
                {
                    ContentLength = null;
                }

                // there is no body, remove any encodings
                TransferEncoding = null;

                using (GetResponseStream(null))
                {
                }
            }

            if (stream != null)
            {
                stream.Close();
            }
        }

        public void Redirect(string url)
        {
            if (IsResponseActive)
            {
                throw new HttpWebServerResponseException("Unable to redirect when a response is already active");
            }

            StatusCode = 307;
            StatusDescription = "Moved";
            Headers["Location"] = url;
            Close();
        }

        public void RawSend(int timeout, byte[] buffer, int bufferDataLength, bool flush = false)
        {
            socket.Send(timeout, buffer, 0, bufferDataLength);

            if (flush)
            {
                socket.Flush();
            }
        }
        #endregion

        #region Private methods
        private byte[] GetHeaders()
        { 
            var headerTextBuffer = headerStringPool.Acquire();

            if (KeepAlive)
            {
                Headers["Keep-Alive"] = "timeout=" + keepAliveTimeout;
            }
            else
            {
                Headers["Keep-Alive"] = null;
            }

            headerTextBuffer.Append("HTTP/");
            headerTextBuffer.Append(Version);
            headerTextBuffer.Append(" ");
            headerTextBuffer.Append(StatusCode.ToString());
            if (!string.IsNullOrEmpty(StatusDescription))
            {
                headerTextBuffer.Append(" ");
                headerTextBuffer.Append(StatusDescription);
            }
            else if (StatusCode == 200)
            {
                headerTextBuffer.Append(" OK");
            }
            headerTextBuffer.Append(HttpWebServer.EndOfLine);

            foreach (var header in Headers)
            {
                headerTextBuffer.Append(header.Key);
                headerTextBuffer.Append(": ");
                headerTextBuffer.Append(header.Value);
                headerTextBuffer.Append(HttpWebServer.EndOfLine);
            }

            headerTextBuffer.Append(HttpWebServer.EndOfLine);

            var bytes = ASCIIEncoding.ASCII.GetBytes(headerTextBuffer.ToString());

            headerStringPool.Release(headerTextBuffer);

            return bytes;
        }
        #endregion
    }
}

