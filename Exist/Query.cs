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

        List<Tag> Tags = new();
        List<Attribute> Attributes = new();

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

        public void AddTags(DateTimeOffset date, IEnumerable<string> tags)
        {
            Tags.AddRange(tags.Select(name => new Tag(date, name)));
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

        public void SetAttributes(DateTimeOffset date, Dictionary<string, int> attributes)
        {
            Attributes.AddRange(attributes.Select(kvp => new Attribute(date, kvp.Key, kvp.Value)));
        }

        public async Task Save()
        {
            if (Tags.Count > 0)
            {
                await Set("attributes/custom/append/",
                    new JArray(
                        Tags.Select(tag =>
                            new JObject(
                                new JProperty("date", tag.Date.ToString("yyyy-MM-dd")),
                                new JProperty("value", tag.Name)
                            )
                        )
                    )
                );
                Tags.Clear();
            }
            if (Attributes.Count > 0)
            {
                foreach (var chunk in Attributes.Chunk(35))
                {
                    await Set("attributes/update/",
                        new JArray(
                            chunk.Select(attribute =>
                                new JObject(
                                    new JProperty("name", attribute.Name),
                                    new JProperty("date", attribute.Date.ToString("yyyy-MM-dd")),
                                    new JProperty("value", attribute.Value)
                                )
                            )
                        )
                    );
                }
                Attributes.Clear();
            }
        }

        record Tag(DateTimeOffset Date, string Name);
        record Attribute(DateTimeOffset Date, string Name, int Value);
    }
}
