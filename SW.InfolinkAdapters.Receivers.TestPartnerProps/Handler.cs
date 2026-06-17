using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SW.PrimitiveTypes;
using SW.Serverless.Sdk;

namespace SW.InfolinkAdapters.Receivers.TestPartnerProps;

public class Handler : IInfolinkReceiver
{
    private const string LoginUsername = "admin@Infolink.systems";
    private const string LoginPassword = "Mtm@dmin!2";

    private readonly List<string> _items = new();

    public Handler()
    {
        Runner.Expect("BaseUrl");
    }

    public Task Initialize() => Task.CompletedTask;
    public Task Finalize() => Task.CompletedTask;

    public async Task<IEnumerable<string>> ListFiles()
    {
        _items.Clear();

        var baseUrl = Runner.StartupValueOf("BaseUrl").TrimEnd('/');
        using var client = new HttpClient();

        // 1. Authenticate and get JWT
        var loginBody = JsonConvert.SerializeObject(new { username = LoginUsername, password = LoginPassword });
        var loginResp = await client.PostAsync(
            $"{baseUrl}/api/accounts/login",
            new StringContent(loginBody, Encoding.UTF8, "application/json"));
        loginResp.EnsureSuccessStatusCode();
        var loginJson = await loginResp.Content.ReadAsStringAsync();
        var jwt = JObject.Parse(loginJson)["jwt"]?.ToString()
            ?? throw new Exception("Login response did not contain 'jwt'.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // 2. Fetch all partners that have ExternalAccountCode configured
        var partnersResp = await client.GetAsync($"{baseUrl}/api/partners/_/withexternalaccountcode");
        partnersResp.EnsureSuccessStatusCode();
        var partnersJson = await partnersResp.Content.ReadAsStringAsync();
        var partners = JArray.Parse(partnersJson);

        // 3. Fetch logistics records from the source API
        var logisticsResp = await client.GetAsync($"{baseUrl}/api/testdata/logistics");
        logisticsResp.EnsureSuccessStatusCode();
        var logisticsJson = await logisticsResp.Content.ReadAsStringAsync();
        var records = JArray.Parse(logisticsJson);

        // 4. Match each logistics record to a partner using ExternalAccountCode + ExternalAccountCodePath
        foreach (var record in records)
        {
            foreach (var partner in partners)
            {
                var path = partner["externalAccountCodePath"]?.ToString();
                var expectedCode = partner["externalAccountCode"]?.ToString();
                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(expectedCode)) continue;

                var actualCode = record.SelectToken(path)?.ToString();
                if (actualCode != expectedCode) continue;

                // Matched — build the payload the NativeUpdatePartnerPropsHandler expects
                var financials = record["financials"];
                var payload = JsonConvert.SerializeObject(new
                {
                    partnerId = partner["id"]!.Value<int>(),
                    properties = new Dictionary<string, string>
                    {
                        ["OutstandingBalance"] = financials?["outstanding_balance"]?.ToString() ?? "",
                        ["LastInvoiceDate"] = financials?["last_invoice_date"]?.ToString() ?? "",
                        ["SyncedAt"] = DateTime.UtcNow.ToString("o")
                    }
                });
                _items.Add(payload);
                break;
            }
        }

        return Enumerable.Range(0, _items.Count).Select(i => i.ToString());
    }

    public Task<XchangeFile> GetFile(string fileId)
    {
        var index = int.Parse(fileId);
        return Task.FromResult(new XchangeFile(_items[index]));
    }

    public Task DeleteFile(string fileId) => Task.CompletedTask;
}

