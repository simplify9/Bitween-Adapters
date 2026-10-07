using DotLiquid;
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

namespace SW.InfolinkAdapters.Handlers.Http
{
    public class Handler : IInfolinkHandler
    {
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

        public Handler()
        {
            Runner.Expect("AuthType", null);
            Runner.Expect("ApiKey", null, true);
            Runner.Expect("LoginUrl", null);
            Runner.Expect("Username", null);
            Runner.Expect("Password", null, true);
            Runner.Expect("Url");
            Runner.Expect("Headers", null);
            Runner.Expect("ContentType", "application/json");
            Runner.Expect("Verb", "post");
            Runner.Expect("DefaultRequest", null);
            Runner.Expect(CommonProperties.ClientId, null);
            Runner.Expect(CommonProperties.ClientSecret, null, true);
        }

        public async Task<XchangeFile> Handle(XchangeFile xchangeFile)
        {
            Options options = new Options();
            HttpClient client = new HttpClient();
            if (options.AuthType == "ApiKey")
                client.DefaultRequestHeaders.Add("ApiKey", options.ApiKey);
            else if (options.AuthType == "Bearer")
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", options.LoginPassword);
            else if (options.AuthType == "Basic")
            {
                string credentials =
                    Convert.ToBase64String(
                        Encoding.ASCII.GetBytes(options.LoginUsername + ":" + options.LoginPassword));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }
            else if (options.AuthType == "Login")
            {
                string loginJson = JsonConvert.SerializeObject(new UserLoginModel()
                {
                    Email = options.LoginUsername,
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
                oauthContentDictionary.Add(new("client_id", options.ClientId));
                oauthContentDictionary.Add(new("client_secret", options.ClientSecret));
                oauthContentDictionary.Add(new("grant_type", "client_credentials"));
                var oauthContent = new FormUrlEncodedContent(oauthContentDictionary);
                oathRequest.Content = oauthContent;
                var oauthResponse = await client.SendAsync(oathRequest);
                var res = await oauthResponse.Content.ReadAsStringAsync();
                var resDeserialized = JsonConvert.DeserializeObject<OAuth2Response>(res);
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", resDeserialized.access_token);
            }

            string requestBody = xchangeFile.Data;
            if (string.IsNullOrEmpty(requestBody))
                requestBody = Runner.StartupValueOf("DefaultRequest");
            string str = options.ContentType.ToLower();
            HttpContent content;
            MultipartFormDataContent multipartTmp;
            byte[] fileContent;
            switch (str)
            {
                case "application/x-www-form-urlencoded":
                    content = new FormUrlEncodedContent(
                        JsonConvert.DeserializeObject<Dictionary<string, string>>(requestBody));
                    break;
                case "multipart/form-data":
                    multipartTmp = new MultipartFormDataContent();
                    fileContent = Encoding.UTF8.GetBytes(requestBody);
                    multipartTmp.Add(new ByteArrayContent(fileContent), "file", xchangeFile.Filename ?? "file");
                    content = multipartTmp;
                    break;
                case "application/json":
                    content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                    break;
                default:
                    content = new StringContent(requestBody, Encoding.UTF8, options.ContentType);
                    break;
            }

            Uri uri;
            if (!string.IsNullOrEmpty(xchangeFile.Data) && options.Url.Contains("{{"))
            {
                string templateData = Runner.StartupValueOf("Url");
                Template parsedTemplate = Template.Parse(templateData);
                IDictionary<string, object> obj =
                    JsonConvert.DeserializeObject<IDictionary<string, object>>(xchangeFile.Data,
                        (JsonConverter)new DictionaryConverter());
                Hash jsonHash = Hash.FromDictionary(obj);
                uri = new Uri(parsedTemplate.Render(jsonHash));
            }
            else
                uri = new Uri(options.Url);

            HttpRequestMessage request = new HttpRequestMessage()
            {
                RequestUri = uri,
                Method = HttpMethodFromString(options.Verb),
                Content = content
            };
            string headers1 = options.Headers;
            IEnumerable<KeyValuePair<string, string>> headers = headers1 != null
                ? (headers1.Split(',')).Select((Func<string, KeyValuePair<string, string>>)(h =>
                {
                    string[] strArray = h.Split(':');
                    return new KeyValuePair<string, string>(strArray[0], strArray[1]);
                }))
                : null;
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> keyValuePair1 in headers)
                {
                    KeyValuePair<string, string> keyValuePair = keyValuePair1;
                    request.Headers.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }

            request.Headers.Add("request-context-correlation-id", options.CorrelationId);
            HttpResponseMessage response = await client.SendAsync(request);
            if (response.StatusCode < HttpStatusCode.OK || response.StatusCode >= HttpStatusCode.InternalServerError)
                throw new Exception(response.StatusCode.ToString());
            string resp = await response.Content.ReadAsStringAsync();
            XchangeFile xchangeFile1 = response.StatusCode < HttpStatusCode.BadRequest
                ? new XchangeFile(resp)
                : new XchangeFile(resp, badData: true);
            return xchangeFile1;
        }
    }
}