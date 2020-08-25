using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Toggl_Exist.Exist
{
    public class Query
    {
        const string Endpoint = "https://exist.io/api/1/";
        const string UserAgent = "Toggl-Exist/1.0";

        readonly string Token;

        HttpClient Client = new HttpClient();

        public Query(string token)
        {
            Token = token;
        }

        internal async Task<JToken> Get(string type)
        {
            var uri = new Uri(Endpoint + type);

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.Clear();
            request.Headers.UserAgent.Add(ProductInfoHeaderValue.Parse(UserAgent));
            request.Headers.Authorization = new AuthenticationHeaderValue("bearer", Token);

            var response = await Client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException(text);
            }
            return JToken.Parse(text);
        }

        internal async Task<JToken> Set(string type, JToken body)
        {
            var uri = new Uri(Endpoint + type);

            var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.UserAgent.Clear();
            request.Headers.UserAgent.Add(ProductInfoHeaderValue.Parse(UserAgent));
            request.Headers.Authorization = new AuthenticationHeaderValue("bearer", Token);
            request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

            var response = await Client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException(text);
            }
            return JToken.Parse(text);
        }

        public async Task<IReadOnlyList<string>> GetTags()
        {
            var data = await Get("users/$self/attributes/?groups=custom&limit=0");
            return data.Children()
                .Where(child => child["priority"].ToObject<int>() >= 2)
                .Select(child => child["label"].ToObject<string>())
                .ToList();
        }

        public async Task AddTags(DateTimeOffset date, IEnumerable<string> tags)
        {
            if (tags.Count() > 0)
            {
                await Set("attributes/custom/append/",
                    new JArray(
                        tags.Select(tag =>
                            new JObject(
                                new JProperty("date", date.ToString("yyyy-MM-dd")),
                                new JProperty("value", tag)
                            )
                        )
                    )
                );
            }
        }

        public async Task AcquireAttributes(IEnumerable<string> attributes)
        {
            await Set("attributes/acquire/",
                new JArray(
                    attributes.Select(attribute =>
                        new JObject(
                            new JProperty("name", attribute),
                            new JProperty("active", true)
                        )
                    )
                )
            );
        }

        public async Task SetAttributes(DateTimeOffset date, Dictionary<string, int> attributes)
        {
            await Set("attributes/update/",
                new JArray(
                    attributes.Select(attribute =>
                        new JObject(
                            new JProperty("name", attribute.Key),
                            new JProperty("date", date.ToString("yyyy-MM-dd")),
                            new JProperty("value", attribute.Value)
                        )
                    )
                )
            );
        }
    }
}
