using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SW.PrimitiveTypes;
using SW.Serverless.Sdk;

namespace SW.InfolinkAdapters.Handlers.Mailgun
{
    public class Handler : IInfolinkHandler
    {
        public Handler()
        {
            Runner.Expect(CommonProperties.ApiKey, false, true);
            Runner.Expect(CommonProperties.Url);
            
        }
        
        public async Task<XchangeFile> Handle(XchangeFile xchangeFile)
        {
            IList<IEnumerable<byte>> attachmentsData = new List<IEnumerable<byte>>();
            MailGunSendRequest mailgunRequest = JsonConvert.DeserializeObject<MailGunSendRequest>(xchangeFile.Data);
            
            
            HttpClient client = new HttpClient();
            string key = Runner.StartupValueOf<string>(CommonProperties.ApiKey);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", key );
            
            MultipartFormDataContent formContent = new MultipartFormDataContent();
            
            formContent.Add(new StringContent(mailgunRequest.To), "to");
            formContent.Add(new StringContent(mailgunRequest.From), "from");
            formContent.Add(new StringContent(mailgunRequest.Subject), "subject");
            
            

            if (mailgunRequest.Template != null)
            {
                formContent.Add(new StringContent(mailgunRequest.Template), "template");
                formContent.Add(new StringContent(JsonConvert.SerializeObject(mailgunRequest.TemplateVariables?? new {})), "h:X-Mailgun-variables");
                
                
            }
            else if (mailgunRequest.Body != null)
            {
                
                formContent.Add(new StringContent(mailgunRequest.Body), "text");
            }
            else
            {
                throw new Exception("Both Body and Template can not be null.");
            }

            if (mailgunRequest.AttachmentLocations != null)
            {
                foreach (var pair in mailgunRequest.AttachmentLocations)
                {
                    Stream stream = await client.GetStreamAsync(pair.Value);
                    formContent.Add(new StreamContent(stream), "attachment", pair.Key);
                }
            }

            string mailgunEndpoint = Runner.StartupValueOf<string>(CommonProperties.Url);
            
            HttpResponseMessage message = await client.PostAsync(mailgunEndpoint, formContent);
            
            return message.IsSuccessStatusCode ? 
                new XchangeFile(await message.Content.ReadAsStringAsync()) 
                : new XchangeFile(await  message.Content.ReadAsStringAsync(), null, true);
        }
    }
}