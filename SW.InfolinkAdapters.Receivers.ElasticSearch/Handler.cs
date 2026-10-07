using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using SW.PrimitiveTypes;
using SW.Serverless.Sdk;
using HttpMethod = System.Net.Http.HttpMethod;
using StringContent = System.Net.Http.StringContent;

namespace SW.InfolinkAdapters.Receivers.ElasticSearch;

public class Handler : IInfolinkReceiver
{
    private HttpClient _client;
    public Handler()
    {
        Runner.Expect(CommonProperties.Url);
        Runner.Expect(CommonProperties.Username);
        Runner.Expect(CommonProperties.Password, false, true);
        Runner.Expect("IndexName");
        Runner.Expect("Query");
        
    }
    
    
    public async Task Initialize()
    {
        _client = new HttpClient();
        
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(Runner.StartupValueOf(CommonProperties.Username) + ":" + Runner.StartupValueOf(CommonProperties.Password)));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    public Task Finalize()
    {
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<string>> ListFiles()
    {
        var requestBody = Runner.StartupValueOf<string>("Query");
        var content =  new StringContent(requestBody, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage()
        {
            RequestUri = new Uri(Runner.StartupValueOf<string>(CommonProperties.Url) + $"/{Runner.StartupValueOf("IndexName")}/_search"),
            Method = HttpMethod.Post,
            Content = content
        };
        var response = await _client.SendAsync(request);
        if (response.StatusCode < HttpStatusCode.OK || response.StatusCode >= HttpStatusCode.InternalServerError)
            throw new Exception(response.StatusCode.ToString());
        var resp = await response.Content.ReadAsStringAsync();
        
        var respJson = JsonConvert.DeserializeObject<SearchResponse>(resp);
        
        var hits = respJson?.Hits?.Hits?.Select(x => x.ToString());

        if (response.StatusCode < HttpStatusCode.BadRequest)
        {
            return hits;
        }
        else
        {
            throw new Exception(resp);
        } 
        
    }

    public async Task<XchangeFile> GetFile(string fileId)
    {
        XchangeFile file = new XchangeFile(fileId);
        return file;
    }

    public Task DeleteFile(string fileId)
    {
        return Task.CompletedTask;
    }
    
    public class SearchResponse
    {
        public int Took { get; set; }
        public RootHits Hits { get; set; }
    }
    public class RootHits
    {
        public int Total { get; set; }
        public float MaxScore { get; set; }
        public List<object> Hits { get; set; }
    }
    
}