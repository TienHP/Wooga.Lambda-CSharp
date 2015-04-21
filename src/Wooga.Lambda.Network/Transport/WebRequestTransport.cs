﻿using System;
using System.IO;
using System.Net;
using Wooga.Lambda.Control.Monad;
using Wooga.Lambda.Data;

namespace Wooga.Lambda.Network.Transport
{
    public static class WebRequestTransport
    {
        public static HttpClient CreteHttpClient()
        {
            return new HttpClient((c,r) => r.RequestHttpResponse().Invoke());    
        }

        private static HttpWebRequest AsWebRequest(this HttpRequest httpRequest)
        {
            var webRequest = WebRequest.Create(httpRequest.Endpoint) as HttpWebRequest;
            webRequest.Headers = httpRequest.HttpHeaders.ToWebHeaders();
            webRequest.Method = httpRequest.HttpMethod.Name;
            using (var postStream = webRequest.GetRequestStream())
            {
                var body = httpRequest.Body.FromJustOrDefault(new byte[0], _ => _);
                postStream.Write(body, 0, body.Length);
                postStream.Close();
            }
            return webRequest;
        }

        private static IO<HttpResponse> RequestHttpResponse(this HttpRequest httpRequest)
        {
            return GetWebResponse(httpRequest).Bind(webResponse => webResponse.AsHttpResponse(httpRequest));
        }

        private static IO<HttpWebResponse> GetWebResponse(HttpRequest httpRequest)
        {
            return () =>
            {
                try
                {
                    return httpRequest.AsWebRequest().GetResponse() as HttpWebResponse;
                }
                catch (WebException webException)
                {
                    return webException.Response as HttpWebResponse;
                }
            };
        }

        private static IO<HttpResponse> AsHttpResponse(this HttpWebResponse response, HttpRequest httpRequest)
        {
            var headers = OfWebHeaders(response.Headers);
            var body = ReadEntireStream(response.GetResponseStream());
            return () => new HttpResponse(httpRequest, headers, response.StatusCode, body.Invoke(), response.ContentType);
        }

        private static IO<String> ReadEntireStream(Stream stream)
        {
            return () => new StreamReader(stream).ReadToEnd();
        }

        private static WebHeaderCollection ToWebHeaders(this HttpHeaders self)
        {
            return self.Headers.Fold((headers, _, header) =>
            {
                headers.Add(header.Key, header.Value);
                return headers;
            },
                new WebHeaderCollection());
        }

        private static HttpWebRequest AppendToRequest(this HttpHeaders self, HttpWebRequest request)
        {
            foreach (var h in self.Headers.Values)
            {
                request.Headers.Add(h.Key, h.Value);
            }
            return request;
        }

        private static HttpHeaders OfWebHeaders(WebHeaderCollection webHeaders)
        {
            return webHeaders.AllKeys.Fold((headers, key) => headers.Append(key, webHeaders.Get(key)),
                HttpHeaders.Create());
        }
    }
}