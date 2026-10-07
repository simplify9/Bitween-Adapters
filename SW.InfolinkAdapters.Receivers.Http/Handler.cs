using Newtonsoft.Json;
using SW.PrimitiveTypes;
using SW.Serverless.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace SW.InfolinkAdapters.Receivers.Http
{
  public class Handler : IInfolinkReceiver
  {
    public Handler()
    {
      Runner.Expect(CommonProperties.AuthType,  null);
      Runner.Expect(CommonProperties.ApiKey, null, true);
      Runner.Expect(CommonProperties.LoginUrl,  null);
      Runner.Expect(CommonProperties.Username,  null);
      Runner.Expect(CommonProperties.Password, null, true);
      Runner.Expect(CommonProperties.Url);
      Runner.Expect(CommonProperties.Headers,  null);
      Runner.Expect(CommonProperties.ClientId,  null);
      Runner.Expect(CommonProperties.ClientSecret, null, true);
      Runner.Expect(CommonProperties.ContentType, "application/json");
      Runner.Expect(CommonProperties.Verb, "get");
      Runner.Expect("DefaultRequest", null);
    }
  
    private HttpMethod HttpMethodFromString(string method)
    {
      switch (method.ToLower())
      {
        case "get":
          return HttpMethod.Get;
        case "delete":
          return HttpMethod.Delete;
        case "put":
          return HttpMethod.Put;
        default:
          return HttpMethod.Post;
      }
    }

    public async Task Initialize()
    {
      var data = await Task.FromResult(new{  });
    }

    public async Task Finalize()
    {
      var data = await Task.FromResult(new{  });
    }

    public async Task<IEnumerable<string>> ListFiles()
    {
      string[] strArray = await Task.FromResult<string[]>(new string[1]
      {
        "1"
      });
      return strArray;
    }

    public async Task<XchangeFile> GetFile(string fileId)
    {
      Options options = new Options();
      HttpClient client = new HttpClient();
      if (options.AuthType == "ApiKey")
        client.DefaultRequestHeaders.Add("ApiKey", options.ApiKey);
      else if (options.AuthType == "Basic")
      {
        string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(options.LoginUsername + ":" + options.LoginPassword));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
      }
      else if (options.AuthType == "Bearer")
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.LoginPassword);
      else if (options.AuthType == "Login")
      {
        string loginJson = JsonConvert.SerializeObject(new UserLoginModel()
        {
          UserName = options.LoginUsername,
          Password = options.LoginPassword
        });
        HttpResponseMessage loginResponse = await client.PostAsync(new Uri(options.LoginUrl),
          new StringContent(loginJson, Encoding.UTF8, "application/json"));
        loginResponse.EnsureSuccessStatusCode();
        if (loginResponse.StatusCode != HttpStatusCode.OK)
          throw new Exception(loginResponse.StatusCode.ToString());
        string rs = await loginResponse.Content.ReadAsStringAsync();
        LoginResponse rsDeserialized = JsonConvert.DeserializeObject<LoginResponse>(rs);
        client.DefaultRequestHeaders.Authorization =
          new AuthenticationHeaderValue("Bearer", rsDeserialized.Jwt);
      }
      else if (options.AuthType == "OAuth2")
      {
        var oathRequest = new HttpRequestMessage(HttpMethod.Post, options.LoginUrl);
        var oauthContentDictionary = new List<KeyValuePair<string, string>>();
        oauthContentDictionary.Add(new KeyValuePair<string, string>("client_id", options.ClientId));
        oauthContentDictionary.Add(new KeyValuePair<string, string>("client_secret", options.ClientSecret));
        oauthContentDictionary.Add(new KeyValuePair<string, string>("grant_type", "client_credentials"));
        var oauthContent = new FormUrlEncodedContent(oauthContentDictionary);
        oathRequest.Content = oauthContent;
        var oauthResponse = await client.SendAsync(oathRequest);
        var res = await oauthResponse.Content.ReadAsStringAsync();
        var resDeserialized = JsonConvert.DeserializeObject<OAuth2Response>(res);
        client.DefaultRequestHeaders.Authorization =
          new AuthenticationHeaderValue("Bearer", resDeserialized.access_token);
      }
      HttpContent content = null;
      if (!string.IsNullOrEmpty(Runner.StartupValueOf("DefaultRequest")))
      {
        string requestBody = Runner.StartupValueOf("DefaultRequest");
        string str = options.ContentType.ToLower();
        switch (str)
        {
          case "application/x-www-form-urlencoded":
            content =  new FormUrlEncodedContent( JsonConvert.DeserializeObject<Dictionary<string, string>>(requestBody));
            break;
          case "application/json":
            content =  new StringContent(requestBody, Encoding.UTF8, "application/json");
            break;
          default:
            content =  new StringContent(requestBody, Encoding.UTF8, options.ContentType);
            break;
        }
      }
      Uri uri = new Uri(options.Url);
      HttpRequestMessage request = new HttpRequestMessage()
      {
        RequestUri = uri,
        Method = HttpMethodFromString(options.Verb),
        Content = content
      };
      string headers1 = options.Headers;
      IEnumerable<KeyValuePair<string, string>> headers = headers1?.Split(',').Select((Func<string, KeyValuePair<string, string>>) (h =>
      {
        string[] strArray = h.Split(':');
        return new KeyValuePair<string, string>(strArray[0], strArray[1]);
      }));
      if (headers != null)
      {
        foreach (KeyValuePair<string, string> keyValuePair1 in headers)
        {
          KeyValuePair<string, string> keyValuePair = keyValuePair1;
          request.Headers.Add(keyValuePair.Key, keyValuePair.Value);
        }
      }
      HttpResponseMessage response = await client.SendAsync(request);
      if (response.StatusCode < HttpStatusCode.OK || response.StatusCode >= HttpStatusCode.InternalServerError)
        throw new Exception(response.StatusCode.ToString());
      string resp = await response.Content.ReadAsStringAsync();
      XchangeFile file = response.StatusCode < HttpStatusCode.BadRequest ? new XchangeFile(resp) : new XchangeFile(resp, badData: true);
      return file;
    }

    public async Task DeleteFile(string fileId)
    {
      var data = await Task.FromResult(new{  });
    }
  }
}
