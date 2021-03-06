// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Azure.Functions.Java.Tests.E2E
{
    public static class Utilities
    {
        public static async Task RetryAsync(Func<Task<bool>> condition, int timeout = 60 * 1000, int pollingInterval = 2 * 1000, bool throwWhenDebugging = false, Func<string> userMessageCallback = null)
        {
            DateTime start = DateTime.Now;
            while (!await condition())
            {
                await Task.Delay(pollingInterval);

                bool shouldThrow = !Debugger.IsAttached || (Debugger.IsAttached && throwWhenDebugging);
                if (shouldThrow && (DateTime.Now - start).TotalMilliseconds > timeout)
                {
                    string error = "Condition not reached within timeout.";
                    if (userMessageCallback != null)
                    {
                        error += " " + userMessageCallback();
                    }
                    throw new ApplicationException(error);
                }
            }
        }

        public static async Task<bool> InvokeHttpTrigger(string functionName, string queryString, HttpStatusCode expectedStatusCode, string expectedMessage, int expectedCode = 0)
        {
            string uri = $"api/{functionName}{queryString}";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(Constants.FunctionsHostUrl);
            var response = await httpClient.SendAsync(request);
            Console.WriteLine($"InvokeHttpTrigger: {functionName}{queryString} : {response.StatusCode} : {response.ReasonPhrase}");
            if (expectedStatusCode != response.StatusCode && expectedCode != (int)response.StatusCode)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(expectedMessage))
            {
                string actualMessage = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"InvokeHttpTrigger: expectedMessage : {expectedMessage}, actualMessage : {actualMessage}");
                return actualMessage.Contains(expectedMessage);
            }
            return true;
        }

        public static async Task<bool> InvokeEventGridTrigger(string functionName, JObject jsonContent, HttpStatusCode expectedStatusCode=HttpStatusCode.Accepted)
        {
            string uri = $"runtime/webhooks/eventgrid?functionName={functionName}";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("aeg-event-type", "Notification");
            request.Content = new StringContent(
                jsonContent.ToString(),
                Encoding.UTF8,
                "application/json"
            );

            var httpClient = new HttpClient {
                BaseAddress = new Uri(Constants.FunctionsHostUrl)
            };

            var response = await httpClient.SendAsync(request);

            if (expectedStatusCode != response.StatusCode) {
                Console.WriteLine($"InvokeEventGridTrigger: expectedStatusCode : {expectedStatusCode}, actualMessage : {response.StatusCode}");
                return false;
            }

            return true;
        }
    }
}
