using NINA.Core.Utility;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.ExoPlanets.Utility {

    public class HttpRequest {

        public static async Task<HttpResponseMessage> HttpRequestAsync(string url, HttpMethod method, CancellationToken ct, string body = "", string contentType = "text/plain") {
            var uri = new Uri(url);

            if (!uri.IsWellFormedOriginalString()) {
                return null;
            }

            var request = new HttpRequestMessage(method, uri);

            if (!string.IsNullOrEmpty(body)) {
                request.Content = new StringContent(body, Encoding.UTF8, contentType);
            }

            Logger.Debug($"Request URL: {request.Method} {request.RequestUri}");
            if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head) {
                Logger.Trace($"Request body:{Environment.NewLine}{request.Content?.ReadAsStringAsync(ct).Result}");
            }

            var client = new HttpClient() {
                Timeout = TimeSpan.FromSeconds(30),
                DefaultRequestHeaders = {
                    { "User-Agent", "NINA ExoPlanet Plugin" }
                },
            };

            HttpResponseMessage response = null;
            int i = 1;

            try {
                response = await client.SendAsync(request, ct);
            } catch (WebException ex) {
                while (i < 4) {
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                    Logger.Error($"HTTP request to {request.RequestUri} failed: {ex.Message}. Retry attempt {i}");

                    response = await client.SendAsync(request, ct);
                    i++;
                }
            }

            client.Dispose();

            Logger.Debug($"Response status code: {response.StatusCode}");
            Logger.Trace($"Response body:{Environment.NewLine}{response.Content?.ReadAsStringAsync(ct).Result}");

            return response;
        }
    }
}