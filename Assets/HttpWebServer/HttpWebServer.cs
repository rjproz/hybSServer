using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

namespace RipcordSoftware.HttpWebServer
{
    public class HttpWebServer : IDisposable
    {
        #region Constants
        public const string HttpVersion10 = "1.0";
        public const string HttpVersion11 = "1.1";

        public const string EndOfLine = "\r\n";

        /// <summary>
        /// The maximum size of a request header.
        /// </summary>
        public const int MaxRequestHeaderSize = 32 * 1024;

        /// <summary>
        /// The maximum size of the chunk encoded response chunk
        /// </summary>
        public const int MaxResponseChunkSize = 2 * 1024;

        /// <summary>
        /// The size of the internal buffer to use for responses
        /// </summary>
        public const int ResponseStreamBufferSize = 32 * 1024;
        #endregion

        #region Types
        public delegate bool RequestCallback(HttpWebRequest request, HttpWebResponse response);
        public delegate bool Request100ContinueCallback(HttpWebRequest request);

        public class Binding
        {
            public Binding(string ip, int port, bool secure)
            {
                var ipOctets = ip.Split('.');
                if (ipOctets == null || ipOctets.Length != 4)
                {
                    throw new HttpWebServerException("Invalid binding address '{0}'", ip);
                }

                IPBytes = new byte[4];
                for (int i = 0; i < IPBytes.Length; i++)
                {
                    try
                    {
                        IPBytes[i] = byte.Parse(ipOctets[i]);
                    }
                    catch (Exception)
                    {
                        throw new HttpWebServerException("Invalid binding address '{0}'", ip);
                    }
                }

                IP = ip;
                Port = port;
                Secure = secure;
                EndPoint = new IPEndPoint(new IPAddress(IPBytes), Port);
            }

            public string IP { get; protected set; }
            public int Port { get; protected set; }
            public bool Secure { get; protected set; }
            public byte[] IPBytes { get; protected set; }
            public IPEndPoint EndPoint { get; protected set; }

            public bool IsWildcardIP { get { return IP == "0.0.0.0"; } }

            public static int GetWildcardIPIndex(Binding[] bindings)
            {
                int index = -1;

                for (int i = 0; bindings != null && index < 0 && i < bindings.Length; i++)
                {
                    if (bindings[i].IsWildcardIP)
                    {
                        index = i;
                    }
                }

                return index;
            }
        }

        public class Config
        {
            public Config(int socketBacklog = 256, int keepAliveTimeout = 30, int keepAliveTimeoutGrace = 5, int receiveTimeoutPeriod = 30, int maxKeepAliveResponses = 100, int maxRequestChunkSize = 16 * 1024)
            {
                SocketBacklog = socketBacklog;
                KeepAliveTimeout = keepAliveTimeout;
                KeepAliveTimeoutGrace = keepAliveTimeoutGrace;
                ReceiveTimeoutPeriod = receiveTimeoutPeriod;
                MaxKeepAliveResponses = maxKeepAliveResponses;
                MaxRequestChunkSize = maxRequestChunkSize;
            }

            public int SocketBacklog { get; protected set; }
            public int KeepAliveTimeout { get; protected set; }
            public int KeepAliveTimeoutGrace { get; protected set; }
            public int KeepAliveTimeoutTotal { get { return KeepAliveTimeout + KeepAliveTimeoutGrace; } }
            public int ReceiveTimeoutPeriod { get; protected set; }
            public int MaxKeepAliveResponses { get; protected set; }
            public int MaxRequestChunkSize { get; protected set; }
        }
        #endregion

        #region Private fields
        private readonly List<Socket> listenSockets;
        private readonly Binding[] bindings;
        private readonly Config config;

        private RequestCallback requestCallback;
        private Request100ContinueCallback requestContinueCallback;

        private readonly static string requestContinueHeader = "HTTP/1.1 100 Continue" + EndOfLine + EndOfLine;
        private readonly static byte[] requestContinueHeaderBytes = Encoding.UTF8.GetBytes(requestContinueHeader);
        #endregion

        #region Constructor
        public HttpWebServer(Binding[] bindings, Config config)
        {
            if (bindings == null || bindings.Length < 1)
            {
                throw new HttpWebServerException("You must supply at least one binding address");
            }
                    
            this.bindings = bindings;
            this.config = config;

            listenSockets = new List<Socket>(bindings.Length);

            foreach (var binding in this.bindings)
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                var hostIP = new IPAddress(binding.IPBytes);
                var ep = new IPEndPoint(hostIP, binding.Port);
                socket.Bind(ep);
                socket.Listen(config.SocketBacklog);

                listenSockets.Add(socket);
            }
        }
        #endregion

        #region Public methods
        public void Start(RequestCallback requestCallback, Request100ContinueCallback requestContinueCallback)
        {
            if (requestCallback != null)
            {
                this.requestCallback = requestCallback;

                if (requestContinueCallback != null)
                {
                    this.requestContinueCallback = requestContinueCallback;
                }
            }

            foreach (var socket in listenSockets)
            {
                AcceptRequestAsync(socket);
            }
        }
        #endregion

        #region Private methods
        private bool AcceptRequestAsync(Socket socket)
        {           
            var args = new SocketAsyncEventArgs();
            args.Completed += AcceptRequest;
            args.UserToken = socket;

            bool accept = socket.AcceptAsync(args);
            return accept;
        }
            
        private void AcceptRequest(object sender, SocketAsyncEventArgs ar)
        {
            // start another thread listening on the same socket while we service the request
            AcceptRequestAsync((Socket)ar.UserToken);

            var socket = new HttpWebSocket(ar.AcceptSocket);
            HandleRequests(socket);
        }

        private void HandleRequests(HttpWebSocket socket)
        {
            var buffer = new byte[4096];

            HttpWebBuffer requestBuffer = null;
            int responseCount = 0;

            try
            {
                while (socket.Connected && responseCount <= config.MaxKeepAliveResponses)
                {
                    // we use the keep-alive timeout if we are waiting for a header to start and the normal receive timeout otherwise
                    var timeout = requestBuffer == null ? config.KeepAliveTimeoutTotal : config.ReceiveTimeoutPeriod;

                    int bufferDataLength = socket.Receive(timeout * 1000, buffer);

                    if (bufferDataLength <= 0)
                    {
                        if (requestBuffer != null)
                        {
                            throw new HttpWebServerReceiveTimeoutException();
                        }
                        else
                        {
                            throw new HttpWebServerHeaderTimeoutException();
                        }
                    }

                    if (requestBuffer == null)
                    {
                        requestBuffer = new HttpWebBuffer(buffer, 0, bufferDataLength);
                        responseCount++;
                    }
                    else
                    {
                        requestBuffer.Append(buffer, 0, bufferDataLength);
                    }

                    // check that the header size is within bounds (including a grace amount)
                    if (requestBuffer.DataLength > (MaxRequestHeaderSize + buffer.Length))
                    {
                        // return Forbidden and close the connection
                        var response = new HttpWebResponse(socket, config.KeepAliveTimeout);
                        response.StatusCode = 403;
                        response.StatusDescription = "Forbidden";
                        response.KeepAlive = false;
                        response.Close();
                    }
                    else
                    {
                        HttpWebRequestHeaders headers;
                        HttpWebBuffer bodyBuffer;
                        if (HttpWebRequestHeaders.GetHeaders(requestBuffer, out headers, out bodyBuffer))
                        {
                            var request = new HttpWebRequest(socket, headers, bodyBuffer, config.ReceiveTimeoutPeriod, config.MaxRequestChunkSize);
                            var response = new HttpWebResponse(socket, config.KeepAliveTimeout);

                            // if we exceed the number of keep-alive responses then we want to close this connection
                            // NOTE: the request handler can override this
                            if (responseCount >= config.MaxKeepAliveResponses)
                            {
                                response.KeepAlive = false;
                            }

                            HandleRequest(request, response);

                            response.Close();

                            requestBuffer.ReleaseBuffers();
                            requestBuffer = null;

                            // if we exceed the number of keep-alive responses but the request handler has reset the KeepAlive flag then we must
                            // set the responseCount back to the limit so the connection isn't just dropped
                            if (responseCount >= config.MaxKeepAliveResponses && response.KeepAlive)
                            {
                                responseCount = config.MaxKeepAliveResponses;
                            }
                        }
                    }
                }
            }
            catch (HttpWebServerHeaderTimeoutException ex)
            {
                // TODO: remove
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            catch (HttpWebServerException ex)
            {
                // TODO: remove
                 System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                // TODO: remove
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            finally
            {
                // if there was an active request then free the buffers
                if (requestBuffer != null)
                {
                    requestBuffer.ReleaseBuffers();
                }

                // if the socket is still connected then we must shut it down
                try
                {
                    if (socket.Connected)
                    {
                        socket.Close();
                    }
                }
                catch { }
            }
        }

        private void HandleRequest(HttpWebRequest request, HttpWebResponse response)
        {
            try
            {
                var responseContinue = true;
                var sentResponse = false;

                if (string.Compare(request.Headers["Expect"], "100-continue", true) == 0)
                {
                    if (requestContinueCallback != null)
                    {
                        responseContinue = requestContinueCallback(request);
                    }

                    if (responseContinue)
                    {
                        response.RawSend(0, requestContinueHeaderBytes, requestContinueHeaderBytes.Length, true);
                    }
                    else
                    {
                        response.StatusCode = 403;
                        response.StatusDescription = "Forbidden";
                        response.Close();
                    }
                }

                if (responseContinue)
                {
                    if (requestCallback != null)
                    {
                        sentResponse = requestCallback(request, response);
                    }

                    if (!sentResponse)
                    {
                        response.StatusCode = 404;
                        response.StatusDescription = "Not Found";
                        response.Close();
                    }
                }
            }
            catch
            {
                try
                {
                    if (!response.IsResponseActive)
                    {
                        response.StatusCode = 500;
                        response.StatusDescription = "Internal Server Error";
                        response.KeepAlive = false;
                        response.Close();
                    }
                }
                catch
                {
                }
            }
        }                     
        #endregion

        #region IDisposable implementation

        public void Dispose()
        {
            foreach (var socket in listenSockets)
            {
                socket.Close();
            }
        }

        #endregion
    }
}

