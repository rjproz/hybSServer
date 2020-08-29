using System;
using System.Net.Sockets;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;

namespace RipcordSoftware.HttpWebServer
{
    public class HttpWebRequest
    {
        #region Types 
        /// <summary>
        /// A class which turns a query string into name/value pairs, and decodes the values if required
        /// </summary>
        public class QueryStringInfo
        {
            #region Private fields
            private readonly SortedDictionary<string, string> queryString = new SortedDictionary<string, string>();
            #endregion

            #region Constructors
            public QueryStringInfo() {}

            public QueryStringInfo(string queryString)
            {
                if (!string.IsNullOrEmpty(queryString))
                {
                    var entities = queryString.Split('&');
                    for (int i = 0; entities != null && i < entities.Length; i++)
                    {
                        var entity = entities[i];
                        if (entity != null)
                        {
                            var parts = entity.Split('=');
                            if (parts != null && parts.Length > 0)
                            {
                                string name = parts[0];
                                string value = string.Empty;
                               
                                if (parts.Length == 2)
                                {
                                    value = parts[1];// System.Web.HttpUtility.UrlDecode(parts[1]);
                                }

                                this.queryString[name] = value;
                            }
                        }
                    }
                }
            }
            #endregion

            #region Public properties
            public string this[string name]
            {
                get
                {
                    string value = null;
                    queryString.TryGetValue(name, out value);
                    return value;
                }
            }

            public string[] Keys
            {
                get
                {
                    var keys = new List<string>(queryString.Keys);
                    return keys.ToArray();
                }
            }

            public override string ToString()
            {
                var queryString = new System.Text.StringBuilder();

                foreach (var pair in this.queryString)
                {
                    if (queryString.Length == 0)
                    {
                        queryString.Append('?');
                    }
                    else
                    {
                        queryString.Append('&');
                    }

                    queryString.AppendFormat("{0}={1}", pair.Key, pair.Value);
                }

                return queryString.ToString();
            }
            #endregion
        }
        #endregion

        #region Private fields
        private HttpWebRequestStream requestStream;

        private readonly System.Net.IPEndPoint localEndPoint;
        private readonly System.Net.IPEndPoint remoteEndPoint;
        #endregion

        #region Constructor
        public HttpWebRequest(HttpWebSocket socket, HttpWebRequestHeaders headers, HttpWebBuffer bodyBuffer, int receiveTimeoutPeriod, int maxRequestChunkSize)
        {
            Headers = headers;
            QueryString = new QueryStringInfo(Headers.QueryString);
            requestStream = new HttpWebRequestStream(socket, bodyBuffer, receiveTimeoutPeriod, maxRequestChunkSize, headers.ContentLength);

            localEndPoint = socket.LocalEndPoint;
            remoteEndPoint = socket.RemoteEndPoint;
        }
        #endregion

        #region Public properties
        public HttpWebRequestHeaders Headers { get; protected set; }

        public Stream GetRequestStream()
        { 
            if (Headers.IsGZipEncoded)
            {
                return new GZipStream(requestStream, CompressionMode.Decompress);
            }
            else if (Headers.IsDeflateEncoded)
            {
                return new DeflateStream(requestStream, CompressionMode.Decompress);
            }
            else
            {
                return requestStream; 
            }
        }

        // TODO: handle https as well here
        public string Scheme { get { return "http"; } }

        public string HttpMethod { get { return Headers.HttpMethod; } }
        public string Uri { get { return Headers.Uri; } }
        public long? ContentLength { get { return Headers.ContentLength; } }
        public string ContentType { get { return Headers.ContentType; } }
        public string Version { get { return Headers.Version; } }
        public string Connection { get { return Headers.Connection; } }
        public string AcceptEncoding { get { return Headers.AcceptEncoding; } }
        public string UserAgent { get { return Headers.UserAgent; } }

        public QueryStringInfo QueryString { get; protected set; }

        public bool KeepAlive
        {
            get
            {
                if (Connection != null)
                {
                    return string.Compare(Connection, "Keep-Alive", true) == 0;
                }
                else if (Version == HttpWebServer.HttpVersion10)
                {
                    return false;
                }
                else if (Version == HttpWebServer.HttpVersion11)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public System.Net.IPEndPoint LocalEndPoint { get { return localEndPoint; } }
        public System.Net.IPEndPoint RemoteEndPoint { get { return remoteEndPoint; } }
        #endregion
    }
}

