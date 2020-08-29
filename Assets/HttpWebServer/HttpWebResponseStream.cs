using System;
using System.IO;
using System.Text;

namespace RipcordSoftware.HttpWebServer
{
    public class HttpWebResponseStream : Stream
    {
        #region Types
        private class ResponseStream : Stream
        {
            #region Private fields
            private HttpWebSocket socket;
            private readonly bool keepSocketAlive;

            private byte[] streamBuffer = HttpWebBufferManager.GetBuffer(HttpWebServer.ResponseStreamBufferSize);
            private int streamBufferPosition = 0;
            private long position = 0;
            #endregion

            #region Constructor
            public ResponseStream(HttpWebSocket socket, bool keepSocketAlive)
            {
                this.socket = socket;
                this.keepSocketAlive = keepSocketAlive;
            }
            #endregion

            #region implemented abstract members of Stream
            public override void Flush()
            {
                if (socket != null)
                {
                    socket.Flush();
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (socket != null)
                {
                    if ((streamBufferPosition + count) >= streamBuffer.Length)
                    {
                        SendBuffer(socket, streamBuffer, 0, streamBufferPosition);
                        SendBuffer(socket, buffer, offset, count);
                        streamBufferPosition = 0;
                    }
                    else
                    {
                        Array.Copy(buffer, offset, streamBuffer, streamBufferPosition, count);
                        streamBufferPosition += count;
                    }

                    position += count;
                }
            }

            public override bool CanRead
            {
                get
                {
                    return false;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return false;
                }
            }
            public override bool CanWrite
            {
                get
                {
                    return true;
                }
            }

            public override long Length
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public override long Position
            {
                get
                {
                    return position;
                }
                set
                {
                    throw new NotImplementedException();
                }
            }
            #endregion

            #region Public methods
            public override void Close()
            {
                if (socket != null)
                {
                    if (streamBufferPosition > 0)
                    {
                        SendBuffer(socket, streamBuffer, 0, streamBufferPosition);
                        position += streamBufferPosition;
                        streamBufferPosition = 0;
                    }

                    Flush();

                    if (!keepSocketAlive)
                    {
                        socket.Close();
                    }

                    socket = null;
                }

                if (streamBuffer != null)
                {
                    HttpWebBufferManager.ReleaseBuffer(streamBuffer);
                    streamBuffer = null;
                }
            }
            #endregion

            #region Private methods
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Close();
                }
            }

            private static int SendBuffer(HttpWebSocket socket, byte[] buffer, int offset, int count)
            {
                var written = 0;

                if (socket != null)
                {
                    while (written < count)
                    {
                        var sentBytes = socket.Send(0, buffer, offset + written, count - written);
                        written += sentBytes;
                    }
                }

                return written;
            }
            #endregion
        }

        private class ChunkedResponseStream : Stream
        {
            #region Private fields
            private static byte[] maxBlockSizeHeader = GetChunkHeader(HttpWebServer.MaxResponseChunkSize);
            private static byte[] endResponseHeader = GetChunkHeader(0);
            private static byte[] endOfLine = Encoding.ASCII.GetBytes(HttpWebServer.EndOfLine);

            private readonly byte[] streamBuffer;

            private readonly ResponseStream stream;
            #endregion

            #region Constructor
            public ChunkedResponseStream(ResponseStream stream)
            {
                this.stream = stream;
                streamBuffer = new byte[maxBlockSizeHeader.Length + HttpWebServer.MaxResponseChunkSize + endOfLine.Length];
            }
            #endregion
                
            #region Public methods
            public override void Write(byte[] buffer, int offset, int count)
            {
                var blocks = count / HttpWebServer.MaxResponseChunkSize;
                var overflow = count % HttpWebServer.MaxResponseChunkSize;

                if (blocks > 0)
                {
                    // copy the chunk header into the stream buffer
                    Array.Copy(maxBlockSizeHeader, streamBuffer, maxBlockSizeHeader.Length);

                    // copy the chunk trailer into the stream buffer
                    Array.Copy(endOfLine, 0, streamBuffer, streamBuffer.Length - endOfLine.Length, endOfLine.Length);

                    for (int i = 0; i < blocks; i++)
                    {
                        // copy in the chunk data
                        Array.Copy(buffer, offset, streamBuffer, maxBlockSizeHeader.Length, HttpWebServer.MaxResponseChunkSize);
                        offset += HttpWebServer.MaxResponseChunkSize;

                        // write the buffer
                        stream.Write(streamBuffer, 0, streamBuffer.Length);
                    }
                }

                if (overflow > 0)
                {
                    // get the chunk overflow header
                    var header = GetChunkHeader(overflow);

                    // copy the header into the stream buffer
                    Array.Copy(header, streamBuffer, header.Length);
                    int overflowLength = header.Length;

                    // copy the chunk body
                    Array.Copy(buffer, offset, streamBuffer, overflowLength, overflow);
                    overflowLength += overflow;

                    // copy the chunk trailer
                    Array.Copy(endOfLine, 0, streamBuffer, overflowLength, endOfLine.Length);
                    overflowLength += endOfLine.Length;

                    // write the overflow data into the socket
                    stream.Write(streamBuffer, 0, overflowLength);
                }                        
            }

            public override void Close()
            {
                // the response finishes with a \r\n
                stream.Write(endResponseHeader, 0, endResponseHeader.Length);

                stream.Close();
            }
            #endregion

            #region Private methods
            private static byte[] GetChunkHeader(int size)
            {
                var format = "{0:X}" + HttpWebServer.EndOfLine + (size == 0 ? HttpWebServer.EndOfLine : string.Empty);
                var text = string.Format(format, size);
                return Encoding.ASCII.GetBytes(text);
            }
            #endregion

            #region implemented abstract members of Stream
            public override void Flush()
            {
                stream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override bool CanRead
            {
                get
                {
                    return false;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return false;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return true;
                }
            }

            public override long Length
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public override long Position
            {
                get
                {
                    return stream.Position;
                }
                set
                {
                    throw new NotImplementedException();
                }
            }
            #endregion

            #region Protected methods
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Close();
                }
            }
            #endregion
        }
        #endregion

        #region Private fields
        private Stream stream;
        #endregion

        #region Constructor
        internal HttpWebResponseStream(HttpWebSocket socket, byte[] headers, bool keepSocketAlive, bool isChunked)
        {
            var responseStream = new ResponseStream(socket, keepSocketAlive);

            responseStream.Write(headers, 0, headers.Length);

            if (isChunked)
            {
                this.stream = new ChunkedResponseStream(responseStream);
            }
            else
            {
                this.stream = responseStream;
            }
        }
        #endregion

        #region implemented abstract members of Stream
        public override void Flush()
        {
            stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            stream.Write(buffer, offset, count);
        }

        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        public override long Length
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override long Position
        {
            get
            {
                return stream.Position;
            }
            set
            {
                throw new NotImplementedException();
            }
        }
        #endregion

        #region Public methods
        public override void Close()
        {
            stream.Close();
        }
        #endregion

        #region Private methods
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Close();
            }
        }
        #endregion
    }
}

