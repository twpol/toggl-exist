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
        const string Endpoint = "https://exist.io/api/2/";
        const string UserAgent = "Toggl-Exist/1.0";

        readonly string Token;
        readonly HttpClient Client = new();
        readonly List<AttributeValue> Attributes = new();

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

        internal async Task<JToken> GetPaginated(string type)
        {
            var results = new List<JToken>();
            var page = await Get(type);
            results.AddRange(page["results"].Children());
            while (page["next"].ToObject<string>() != null)
            {
                page = await Get(page["next"].ToObject<string>()[Endpoint.Length..]);
                results.AddRange(page["results"].Children());
            }
            return new JArray(results.ToArray());
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

        public async Task<IReadOnlyList<Attribute>> GetTags()
        {
            var data = await GetPaginated("attributes/?groups=custom&limit=100");
            return data.Children()
                .Where(child => child["priority"].ToObject<int>() >= 2)
                .Select(child => new Attribute(child["name"].ToObject<string>(), child["label"].ToObject<string>()))
                .ToList();
        }

        public void SetAttributes(DateTimeOffset date, Dictionary<string, int> attributes)
        {
            Attributes.AddRange(attributes.Select(kvp => new AttributeValue(date, kvp.Key, kvp.Value)));
        }

        public async Task Save()
        {
            try
            {
                await SaveAttributes();
            }
            catch (InvalidOperationException)
            {
                await AcquireAttributes();
                await SaveAttributes();
            }
        }

        async Task AcquireAttributes()
        {
            foreach (var chunk in Attributes.Chunk(35))
            {
                await Set("attributes/acquire/",
                    new JArray(
                        chunk.Select(attribute =>
                            new JObject(
                                new JProperty("name", attribute.Name)
                            )
                        )
                    )
                );
            }
        }

        async Task SaveAttributes()
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

        public record Attribute(string Name, string Label);

        record AttributeValue(DateTimeOffset Date, string Name, int Value);
    }
}
