using System;
using System.Collections.Generic;

namespace RipcordSoftware.HttpWebServer
{
    public static class HttpWebMimeTypes
    {
        #region Types
        public class ContentTypeInfo
        {
            public ContentTypeInfo(string extension, string contentType, bool compressible) : this(new string[] { extension }, contentType, compressible)
            {
            }

            public ContentTypeInfo(string[] extensions, string contentType, bool compressible)
            {
                Extensions = extensions;
                ContentType = contentType;
                Compressible = compressible;
            }

            public string[] Extensions { get; protected set; }

            public string ContentType { get; protected set; }

            public bool Compressible { get; protected set; }
        }
        #endregion

        #region Private fields
        private static readonly ContentTypeInfo[] contentTypeInfo = new ContentTypeInfo[] {
            new ContentTypeInfo("jpg", @"image/jpg", false),
            new ContentTypeInfo("png", @"image/png", false),
            new ContentTypeInfo("gif", @"image/gif", false),
            new ContentTypeInfo("ico", @"image/ico", true),
            new ContentTypeInfo("bmp", @"image/x-ms-bmp", true),
            new ContentTypeInfo(new string[] { "html", "htm" }, @"text/html", true),
            new ContentTypeInfo("css", @"text/css", true),
            new ContentTypeInfo("js", @"text/javascript", true),
            new ContentTypeInfo("txt", @"text/plain", true),
            new ContentTypeInfo("xml", @"text/xml", true),
            new ContentTypeInfo("woff", @"application/font-woff", false),
            new ContentTypeInfo("svg", @"image/svg+xml", true),
            new ContentTypeInfo("json", @"application/json", true) };
            
        private static readonly Dictionary<string, ContentTypeInfo> extnLookup;
        private static readonly Dictionary<string, ContentTypeInfo> contentTypeLookup;
        #endregion

        #region Constructor
        static HttpWebMimeTypes()
        {
            extnLookup = new Dictionary<string, ContentTypeInfo>(contentTypeInfo.Length);
            contentTypeLookup = new Dictionary<string, ContentTypeInfo>(contentTypeInfo.Length);

            foreach (var info in contentTypeInfo)
            {
                foreach (var extn in info.Extensions)
                {
                    extnLookup.Add(extn, info);
                }

                contentTypeLookup.Add(info.ContentType, info);
            }
        }
        #endregion

        #region Public methods
        public static ContentTypeInfo LookupByExtn(string uri)
        {
            ContentTypeInfo info = null;

            if (!string.IsNullOrEmpty(uri))
            {
                // remove the query string if we have one
                var queryStringIndex = uri.IndexOf('?');
                if (queryStringIndex > 0)
                {
                    uri = uri.Substring(0, queryStringIndex);
                }

                var extnIndex = uri.LastIndexOf('.');
                if (extnIndex > 0 && uri.Length > extnIndex)
                {
                    var extn = uri.Substring(extnIndex + 1);

                    extnLookup.TryGetValue(extn, out info);
                }
            }

            return info;
        }

        public static ContentTypeInfo LookupByContentType(string type)
        {
            ContentTypeInfo info = null;

            if (!string.IsNullOrEmpty(type))
            {
                int encodingStartIndex = type.IndexOf(';');
                if (encodingStartIndex > 0)
                {
                    type = type.Substring(0, encodingStartIndex);
                    type = type.TrimEnd();
                }

                contentTypeLookup.TryGetValue(type, out info);
            }

            return info;
        }
        #endregion
    }
}

