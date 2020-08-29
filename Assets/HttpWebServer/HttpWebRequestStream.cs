using System;
using System.Net.Sockets;
using System.IO;

namespace RipcordSoftware.HttpWebServer
{
    public class HttpWebRequestStream : Stream
    {    
        #region Types
        private class RequestStream : Stream
        {        
            #region Private fields
            private readonly HttpWebSocket socket;
            private MemoryStream bodyStream;
            private readonly int receiveTimeoutPeriod;
            private readonly long? contentLength;
            private long position = 0;
            #endregion

            public RequestStream(HttpWebSocket socket, HttpWebBuffer bodyBuffer, int receiveTimeoutPeriod, long? contentLength)
            {
                this.socket = socket;
                this.receiveTimeoutPeriod = receiveTimeoutPeriod;

                if (bodyBuffer != null)
                {
                    bodyStream = new MemoryStream(bodyBuffer.Buffer, 0, bodyBuffer.DataLength, false);
                }

                this.contentLength = contentLength;
            }

            #region implemented abstract members of Stream

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {        
                return Read(buffer, offset, count, false);
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
                throw new NotImplementedException();
            }

            public override bool CanRead
            {
                get
                {
                    return true;
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
                    return false;
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
            public int Read(byte[] buffer, int offset, int count, bool peek)
            {        
                int bytesRead = 0;

                if (bodyStream != null)
                {
                    bytesRead = bodyStream.Read(buffer, offset, count);

                    offset += bytesRead;
                    count -= bytesRead;

                    if (peek)
                    {
                        bodyStream.Position -= bytesRead;
                    }
                    else
                    {
                        position += bytesRead;

                        if (bodyStream.Position >= bodyStream.Length)
                        {
                            bodyStream = null;
                        }
                    }
                }

                // if we have a content length then calculate if we are at the end of the request or not
                bool endOfRequest = contentLength.HasValue && position >= contentLength.Value;

                if (count > 0 && !endOfRequest)
                {
                    var flags = peek ? SocketFlags.Peek : SocketFlags.None;

                    // if we have already read some bytes on this pass then we don't need a timeout
                    var timeout = bytesRead > 0 ? 0 : receiveTimeoutPeriod * 1000;

                    var socketBytes = socket.Receive(timeout, buffer, offset, count, flags);

                    // if we have a timeout value and no bytes then we should throw
                    if (timeout > 0 && socketBytes <= 0)
                    {
                        throw new HttpWebServerReceiveTimeoutException();
                    }

                    // if we read something from the socket then move our counters
                    if (socketBytes > 0)
                    {
                        bytesRead += socketBytes;
                        position += socketBytes;
                    }
                }

                return bytesRead;
            }
            #endregion
        }

        private class ChunkedRequestStream : RequestStream
        {
            #region Types
            private class ChunkHeader
            {
                public ChunkHeader(int blockSize, int headerSize)
                {
                    BlockSize = blockSize;
                    HeaderSize = headerSize;
                }

                public int BlockSize { get; protected set; }
                public int HeaderSize { get; protected set; }
            }
            #endregion

            #region Private fields
            private static readonly byte[] lastChunkSig = new byte[] { 0x30, 0x0d, 0x0a, 0x0d, 0x0a };

            private MemoryStream chunkStream;

            private readonly int maxRequestChunkSize;
            #endregion

            #region Constuctor
            public ChunkedRequestStream(HttpWebSocket socket, HttpWebBuffer bodyBuffer, int receiveTimeoutPeriod, int maxRequestChunkSize) : base(socket, bodyBuffer, receiveTimeoutPeriod, null)
            {
                this.maxRequestChunkSize = maxRequestChunkSize;
            }
            #endregion

            #region implemented abstract members of Stream

            public override void Flush()
            {
                base.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int bytesRead = 0;

                if (chunkStream == null || chunkStream.Position == chunkStream.Length)
                {
                    if (chunkStream != null)
                    {
                        HttpWebBufferManager.ReleaseMemoryStream(ref chunkStream);
                    }

                    chunkStream = GetChunk();
                }

                if (chunkStream != null)
                {
                    bytesRead = chunkStream.Read(buffer, offset, count);
                }

                return bytesRead;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return base.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                base.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                base.Write(buffer, offset, count);
            }

            public override bool CanRead
            {
                get
                {
                    return base.CanRead;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return base.CanSeek;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return base.CanWrite;
                }
            }

            public override long Length
            {
                get
                {
                    return base.Length;
                }
            }

            public override long Position
            {
                get
                {
                    return base.Position;
                }
                set
                {
                    base.Position = value;
                }
            }

            #endregion

            #region Private methods
            private MemoryStream GetChunk()
            {
                MemoryStream chunkStream = null;

                var tempBuffer = new byte[16];
                var tempBufferDataLength = 0;

                do
                {
                    tempBufferDataLength = base.Read(tempBuffer, 0, tempBuffer.Length, true);
                } while (tempBufferDataLength < tempBuffer.Length && !IsLastChunk(tempBuffer, tempBufferDataLength));

                if (tempBufferDataLength > 0)
                {
                    var chunkHeader = GetChunkHeader(tempBuffer, tempBufferDataLength, maxRequestChunkSize);

                    // eat the header since we know the size now
                    base.Read(tempBuffer, 0, chunkHeader.HeaderSize);

                    if (chunkHeader.BlockSize > 0)
                    {
                        chunkStream = HttpWebBufferManager.GetMemoryStream(chunkHeader.BlockSize);
                        var chunkBuffer = chunkStream.GetBuffer();

                        int bytesRead = 0;
                        do
                        {                            
                            bytesRead += base.Read(chunkBuffer, bytesRead, chunkHeader.BlockSize - bytesRead);
                        } while (bytesRead < chunkHeader.BlockSize);                                                        
                    }

                    // we are at the end of the block, eat the trailing \r\n
                    base.Read(tempBuffer, 0, 2);
                }

                return chunkStream;
            }

            private static ChunkHeader GetChunkHeader(byte[] buffer, int dataLength, int maxRequestChunkSize)
            {
                ChunkHeader header = null;
                int i = 0;

                dataLength = Math.Min(buffer.Length, dataLength);

                // skip the trailing end of the previous block
                if ((dataLength > 0 && buffer[0] == '\n'))
                {
                    i++;
                }
                else if (dataLength > 0 && buffer[0] == '\r' && buffer[1] == '\n')
                {
                    i += 2;
                }
                    
                int length = 0;
                for (; i < dataLength && buffer[i] != '\r' && buffer[i] != '\n'; i++)
                {
                    length *= 16;

                    var value = buffer[i];
                    if (value >= '0' && value <= '9')
                    {
                        value -= 48;
                    }
                    else if (value >= 'a' && value <= 'f')
                    {
                        value -= (97 - 10);
                    }
                    else if (value >= 'A' && value <= 'F')
                    {
                        value -= (65 - 10);
                    }
                    else
                    {
                        throw new HttpWebServerException("The request chunk data is malformed");
                    }

                    length += value;
                }

                if (length > maxRequestChunkSize)
                {
                    throw new HttpWebServerException("The request chunk size ({0}) is too large", length);
                }

                if (buffer[i] == '\n' || (buffer[i++] == '\r' && i < dataLength && buffer[i] == '\n'))
                {
                    header = new ChunkHeader(length, i + 1);
                }
                else
                {               
                    throw new HttpWebServerException("The request chunk data is malformed");
                }

                return header;
            }

            private bool IsLastChunk(byte[] buffer, int dataLength)
            {
                dataLength = Math.Min(buffer.Length, dataLength);

                int sigIndex = 0;
                for (int i = 0; i < dataLength; i++)
                {
                    if (buffer[i] == lastChunkSig[sigIndex])
                    {
                        sigIndex++;
                    }
                    else
                    {
                        sigIndex = 0;
                    }
                }

                return sigIndex == lastChunkSig.Length;
            }
            #endregion
        }
        #endregion

        #region Private fields       
        private readonly RequestStream stream;
        #endregion

        public HttpWebRequestStream(HttpWebSocket socket, HttpWebBuffer bodyBuffer, int receiveTimeoutPeriod, int maxRequestChunkSize, long? contentLength)
        {
            if (!contentLength.HasValue)
            {
                stream = new ChunkedRequestStream(socket, bodyBuffer, receiveTimeoutPeriod, maxRequestChunkSize);
            }
            else
            {
                stream = new RequestStream(socket, bodyBuffer, receiveTimeoutPeriod, contentLength);
            }                
        }

        #region implemented abstract members of Stream

        public override void Flush()
        {
            stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {        
            return stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            stream.Write(buffer, offset, count);
        }

        public override bool CanRead
        {
            get
            {
                return stream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return stream.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return stream.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                return stream.Length;
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
                stream.Position = value;
            }
        }

        #endregion
    }
}

