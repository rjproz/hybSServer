using System;

namespace RipcordSoftware.HttpWebServer
{
    public class HttpWebBuffer : IDisposable
    {
        #region Private fields
        private byte[] buffer;
        private int dataLength;
        #endregion

        #region Constructor
        public HttpWebBuffer(byte[] buffer, int bufferOffset, int dataLength)
        {
            if (bufferOffset + dataLength - buffer.Length > 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            this.buffer = HttpWebBufferManager.GetBuffer(dataLength);
            this.dataLength = dataLength;

            Array.Copy(buffer, bufferOffset, this.buffer, 0, dataLength);
        }
        #endregion

        #region Public methods
        public void Append(byte[] appendBuffer, int appendBufferOffset, int appendDataLength)
        {
            var newDataLength = dataLength + appendDataLength;

            if (newDataLength > buffer.Length)
            {
                var newBuffer = HttpWebBufferManager.GetBuffer(newDataLength);

                Array.Copy(buffer, 0, newBuffer, 0, dataLength);
                Array.Copy(appendBuffer, appendBufferOffset, newBuffer, dataLength, appendDataLength);

                HttpWebBufferManager.ReleaseBuffer(buffer);

                buffer = newBuffer;
                dataLength = newDataLength;
            }
            else
            {
                Array.Copy(appendBuffer, appendBufferOffset, buffer, dataLength, appendDataLength);
                dataLength += appendDataLength;
            }
        }

        public void ReleaseBuffers()
        {
            if (buffer != null)
            {
                HttpWebBufferManager.ReleaseBuffer(buffer);
                buffer = null;
                dataLength = 0;
            }
        }
        #endregion

        #region Public properties
        public byte this[int index]
        {
            get
            {
                if (index < 0 || index >= dataLength)
                {
                    throw new IndexOutOfRangeException();
                }

                return buffer[index];
            }
        }
            
        public int DataLength { get { return dataLength; } }

        public byte[] Buffer { get { return buffer; } }
        public int BufferLength { get { return buffer.Length; } }
        #endregion

        #region IDisposable implementation

        public void Dispose()
        {
            ReleaseBuffers();
        }

        #endregion
    }
}

