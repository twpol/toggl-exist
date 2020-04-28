using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Toggl_Exist.Toggl
{
    public class Query
    {
        const string Endpoint = "https://toggl.com/reports/api/v2/";
        const string UserAgent = "Toggl-Exist/1.0";

        readonly string Token;
        readonly string Workspace;

        HttpClient Client = new HttpClient();

        public Query(string token, string workspace)
        {
            Token = token;
            Workspace = workspace;
        }

        internal async Task<JObject> Get(string type, Dictionary<string, string> query)
        {
            var uri = new Uri(
                Endpoint +
                type +
                QueryHelpers.AddQueryString(
                    QueryHelpers.AddQueryString("", query),
                    new Dictionary<string, string> {
                        { "user_agent", UserAgent },
                        { "workspace_id", Workspace },
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
            return JObject.Parse(text);
        }

        public async Task<IReadOnlyList<TimeEntry>> GetDetails()
        {
            var response = await Get("details", new Dictionary<string, string>());
            return response["data"].ToObject<TimeEntry[]>();
        }
    }
}
