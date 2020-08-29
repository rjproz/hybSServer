using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;

namespace RipcordSoftware.HttpWebServer
{
    /// <summary>
    /// A simple buffer manager class to reduce GC workload for applications where buffer allocation is common operation
    /// </summary>
    public static class HttpWebBufferManager
    {
        #region Constants
        private const int smallBufferLimit = 4 * 1024;
        private const int mediumBufferLimit = 32 * 1024;
        private const int largeBufferLimit = 256 * 1024;
        private const int maxBufferLimit = 512 * 1024;

        private const int maxSmallBuffers = 512;
        private const int maxMediumBuffers = 256;
        private const int maxLargeBuffers = 64;
        #endregion

        #region Private fields
        private readonly static ConcurrentQueue<byte[]> smallBuffers = new ConcurrentQueue<byte[]>();
        private readonly static ConcurrentQueue<byte[]> mediumBuffers = new ConcurrentQueue<byte[]>();
        private readonly static ConcurrentQueue<byte[]> largeBuffers = new ConcurrentQueue<byte[]>();
        #endregion

        #region Public methods
        /// <summary>
        /// Gets the buffer from the manager
        /// </summary>
        public static byte[] GetBuffer(int length)
        {
            byte[] buffer = null;

            if (length >= 0)
            {
                var buffers = smallBuffers;

                if (length > smallBufferLimit)
                {
                    if (length <= mediumBufferLimit)
                    {
                        buffers = mediumBuffers;
                    }
                    else if (length <= largeBufferLimit)
                    {
                        buffers = largeBuffers;
                    }
                }
                    
                if (buffers != null && buffers.Count > 0)
                {
                    buffers.TryDequeue(out buffer);
                }

                if (buffer == null)
                {
                    buffer = new byte[length];
                }
            }

            return buffer;
        }

        public static MemoryStream GetMemoryStream(int length)
        {
            MemoryStream stream = null;

            var buffer = GetBuffer(length);
            if (buffer != null)
            {
                stream = new MemoryStream(buffer, 0, length, true, true);
            }

            return stream; 
        }

        /// <summary>
        /// Returns a buffer for reuse by the manager
        /// </summary>
        public static bool ReleaseBuffer(byte[] buffer)
        {
            bool released = false;

            if (buffer != null)
            {
                var length = buffer.Length;
                if (length <= maxBufferLimit)
                {
                    ConcurrentQueue<byte[]> buffers = null;
                    int maxBufferLength = 0;

                    if (length >= largeBufferLimit)
                    {
                        buffers = largeBuffers;
                        maxBufferLength = maxLargeBuffers;
                    }
                    else if (length >= mediumBufferLimit)
                    {
                        buffers = mediumBuffers;
                        maxBufferLength = maxMediumBuffers;
                    }
                    else if (length >= smallBufferLimit)
                    {
                        buffers = smallBuffers;
                        maxBufferLength = maxSmallBuffers;
                    }

                    if (buffers != null && buffers.Count < maxBufferLength)
                    {
                        buffers.Enqueue(buffer);
                        released = true;
                    }
                }
            }

            return released;
        }

        /// <summary>
        /// Returns a set of buffers to the manager
        /// </summary>
        /// <param name="buffers">Buffers.</param>
        public static void ReleaseBuffers(List<byte[]> buffers)
        {
            if (buffers != null)
            {
                foreach (var buffer in buffers)
                {
                    ReleaseBuffer(buffer);
                }
            }
        }

        public static bool ReleaseMemoryStream(ref MemoryStream stream)
        {
            bool released = false;

            if (stream != null)
            {
                var buffer = stream.GetBuffer();
                if (buffer != null && buffer.Length > 0)
                {
                    released = ReleaseBuffer(buffer);
                    if (released)
                    {
                        stream = null;
                    }
                }
            }

            return released;
        }

        #endregion
    }
}

