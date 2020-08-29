using System;

namespace RipcordSoftware.HttpWebServer
{
    [Serializable]
    public class HttpWebServerException : ApplicationException
    {
        public HttpWebServerException() : base() {}
        public HttpWebServerException(string msg, params object[] args) : base(string.Format(msg, args)) {}
        public HttpWebServerException(string msg, Exception innerException) : base(msg, innerException) {}
    }

    [Serializable]
    public class HttpWebServerReceiveTimeoutException : HttpWebServerException
    {
        public HttpWebServerReceiveTimeoutException() : base("A socket receive timeout occured") {}
        public HttpWebServerReceiveTimeoutException(string msg, params object[] args) : base(string.Format(msg, args)) {}
        public HttpWebServerReceiveTimeoutException(string msg, Exception innerException) : base(msg, innerException) {}
    }

    [Serializable]
    public class HttpWebServerHeaderTimeoutException : HttpWebServerException
    {
        public HttpWebServerHeaderTimeoutException() : base("A socket header receive timeout occured") {}
        public HttpWebServerHeaderTimeoutException(string msg, params object[] args) : base(string.Format(msg, args)) {}
        public HttpWebServerHeaderTimeoutException(string msg, Exception innerException) : base(msg, innerException) {}
    }

    [Serializable]
    public class HttpWebServerResponseException : HttpWebServerException
    {
        public HttpWebServerResponseException() : base() {}
        public HttpWebServerResponseException(string msg, params object[] args) : base(string.Format(msg, args)) {}
        public HttpWebServerResponseException(string msg, Exception innerException) : base(msg, innerException) {}
    }
}

