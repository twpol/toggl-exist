using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Toggl_Exist.Toggl
{
    public class Query
    {
        const string Endpoint = "https://api.track.toggl.com/reports/api/v2/";
        const string UserAgent = "Toggl-Exist/1.0";

        readonly string Token;
        readonly IList<string> Workspaces;

        HttpClient Client = new HttpClient();

        public Query(string token, IList<string> workspaces)
        {
            Token = token;
            Workspaces = workspaces;
        }

        internal async Task<JToken> Get(string type, string workspace, IDictionary<string, string> query)
        {
            var uri = new Uri(
                Endpoint +
                type +
                QueryHelpers.AddQueryString(
                    QueryHelpers.AddQueryString("", query),
                    new Dictionary<string, string> {
                        { "user_agent", UserAgent },
                        { "workspace_id", workspace },
                        { "order_field", "date" },
                        { "order_desc", "on" },
                    }
                )
            );

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.Clear();
            request.Headers.UserAgent.Add(ProductInfoHeaderValue.Parse(UserAgent));
            request.Headers.Authorization = new AuthenticationHeaderValue("basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Token}:api_token")));

            var response = await Client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            return JToken.Parse(text);
        }

        public async Task<IReadOnlyList<TimeEntry>> GetDetails(IReadOnlyList<string> matchingTags, Dictionary<string, string> query)
        {
            var entries = new SortedList<DateTimeOffset, TimeEntry>();
            foreach (var workspace in Workspaces)
            {
                var page = 1;
                while (true)
                {
                    query["page"] = page.ToString();
                    var response = await Get("details", workspace, query);
                    var total_count = response["total_count"].ToObject<int>();
                    var per_page = response["per_page"].ToObject<int>();
                    foreach (var entry in response["data"].ToObject<List<TimeEntry>>())
                    {
                        entries.Add(entry.start, entry);
                    }
                    if (total_count <= per_page * page) break;
                    page++;
                }
            }
            foreach (var entry in entries.Values)
            {
                entry.matchingTags = entry.tags.Where(tag => matchingTags.Contains(tag, StringComparer.CurrentCultureIgnoreCase)).ToList();
            }
            return entries.Values.Reverse().ToList();
        }
    }
}
