using System;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

namespace RipcordSoftware.HttpWebServer
{
    public class HttpWebSocket
    {
        #region Private fields
        /// <summary>
        /// The TCP socket
        /// </summary>
        private Socket socket;

        /// <summary>
        /// A locking object for the TCP socket
        /// </summary>
        private readonly object socketLock = new object();

        /// <summary>
        /// An event we use to synchronize with an asynchronous socket send response
        /// </summary>
        private readonly AutoResetEvent completedEvent = new AutoResetEvent(false);

        private readonly System.Net.IPEndPoint localEndPoint;
        private readonly System.Net.IPEndPoint remoteEndPoint;
        #endregion

        #region Constructors
        public HttpWebSocket(Socket socket)
        {
            localEndPoint = (System.Net.IPEndPoint)socket.LocalEndPoint;
            remoteEndPoint = (System.Net.IPEndPoint)socket.RemoteEndPoint;

            this.socket = socket;
        }
        #endregion

        #region Public methods
        public void Close()
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Disconnect(true);
        }

        public int Send(int timeout, byte[] buffer, int offset, int length)
        {
            return Send(timeout, buffer, offset, length, SocketFlags.None);
        }

        public int Send(int timeout, byte[] buffer, int offset, int count, SocketFlags flags)
        {
            int sentBytes = -1;

            if (timeout > 0)
            {
                lock (socketLock)
                {
                    var args = new SocketAsyncEventArgs();
                    args.SetBuffer(buffer, offset, count);
                    args.SocketFlags = flags;

                    args.Completed += (object sender, SocketAsyncEventArgs e) =>
                    {
                        if (e.BytesTransferred > 0)
                        {
                            completedEvent.Set();
                        }
                    };                

                    socket.SendAsync(args);

                    if (completedEvent.WaitOne(timeout))
                    {
                        sentBytes = args.BytesTransferred;
                    }
                }
            }
            else
            {
                lock (socketLock)
                {
                    sentBytes = SyncSend(socket, buffer, offset, count, flags);
                }
            }

            return sentBytes;
        }

        public int Receive(int timeout, byte[] buffer)
        {
            return Receive(timeout, buffer, 0, buffer.Length);
        }

        public int Receive(int timeout, byte[] buffer, int offset, int length)
        {
            return Receive(timeout, buffer, offset, length, SocketFlags.None);
        }

        public int Receive(int timeout, byte[] buffer, int offset, int count, SocketFlags flags)
        {
            int receivedBytes = 0;

            try
            {
                if (timeout > 0)
                {
                    socket.ReceiveTimeout = timeout;
                    receivedBytes = socket.Receive(buffer, offset, count, flags);
                }
                else if (timeout == 0)
                {
                    var available = socket.Available;
                    if (available > 0)
                    {
                        socket.ReceiveTimeout = -1;
                        var receiveBytes = Math.Min(available, count);
                        receivedBytes = socket.Receive(buffer, offset, receiveBytes, flags);
                    }
                }
                else
                {
                    socket.ReceiveTimeout = -1;
                    receivedBytes = socket.Receive(buffer, offset, count, flags);
                }
            }
            catch (SocketException ex)
            {
                // pass on all except blocking errors
                if (ex.SocketErrorCode != SocketError.WouldBlock)
                    throw;
            }

            return receivedBytes;
        }

        public void Flush()
        {
            if (NoDelay == false)
            {
                NoDelay = true;
                NoDelay = false;
            }
        }
        #endregion

        #region Public properties
        public bool Connected { get { return socket.Connected; } }
        public int Available { get { return socket.Available; } }
        public bool NoDelay { get { return socket.NoDelay; } set { socket.NoDelay = value; } }

        public System.Net.IPEndPoint LocalEndPoint { get { return localEndPoint; } }
        public System.Net.IPEndPoint RemoteEndPoint { get { return remoteEndPoint; } }
        #endregion

        #region Private methods
        /// <summary>
        /// Sends the buffer synchronously to the remote, guarantees the buffer is sent or an exception will be thrown by the socket layer
        /// </summary>
        private static int SyncSend(Socket socket, byte[] buffer, int offset, int count, SocketFlags flags)
        {
            int sentBytes = 0;

            while (sentBytes < count)
            {
                sentBytes += socket.Send(buffer, offset + sentBytes, count - sentBytes, flags);
            }

            return sentBytes;
        }
        #endregion
    }
}

