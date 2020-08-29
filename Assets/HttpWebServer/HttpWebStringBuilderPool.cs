using System;
using System.Text;
using System.Threading;

namespace RipcordSoftware.HttpWebServer
{
    /// <summary>
    /// A *simple* StringBuilder instance pool
    /// </summary>
    public class HttpWebStringBuilderPool
    {
        #region Private fields
        private readonly StringBuilder[] pool;
        private int index = 0;
        private readonly int stringLength;
        #endregion

        #region Constructor
        public HttpWebStringBuilderPool(int poolSize = 16, int stringLength = 1024)
        {
            pool = new StringBuilder[poolSize];
            this.stringLength = stringLength;

            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = new StringBuilder(this.stringLength);
            }
        }
        #endregion

        #region Public methods
        /// <summary>
        /// Get an instance of StringBuilder from the pool
        /// </summary>
        public StringBuilder Acquire()
        {
            int thisIndex = Interlocked.Increment(ref index) - 1;
            thisIndex %= pool.Length;

            var builder = Interlocked.Exchange(ref pool[thisIndex], null);
            if (builder == null)
            {
                builder = new StringBuilder(stringLength);
            }

            // make sure the builder is clear
            builder.Clear();

            return builder;
        }

        /// <summary>
        /// Release an instance of StringBuilder back into the pool
        /// </summary>
        /// <param name="builder">Builder.</param>
        public bool Release(StringBuilder builder)
        {
            bool released = false;

            if (builder != null)
            {                   
                // get the current index into the pool
                int currIndex = index % pool.Length;

                // the most likely null entry in the pool is the last entry we were at, so try there first
                if (currIndex > 0)
                {
                    currIndex--;
                }

                for (int i = currIndex; !released && i < pool.Length; i++)
                {
                    if (pool[i] == null)
                    {
                        released = Interlocked.CompareExchange(ref pool[i], builder, null) == null;
                    }
                }

                for (int i = 0; !released && i < currIndex; i++)
                {
                    if (pool[i] == null)
                    {
                        released = Interlocked.CompareExchange(ref pool[i], builder, null) == null;
                    }
                }
            }

            return released;
        }
        #endregion
    }
}

