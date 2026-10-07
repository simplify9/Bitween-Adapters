using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SendGrid;
using SendGrid.Helpers.Mail;
using SW.PrimitiveTypes;
using SW.Serverless.Sdk;

namespace SW.InfolinkAdapters.Handlers.Sendgrid
{
    public class Handler: IInfolinkHandler
    {
        public Handler()
        {
            Runner.Expect(CommonProperties.ApiKey, false, true);
            Runner.Expect(CommonProperties.InputModel,"EmailRequest");
            Runner.Expect(CommonProperties.From,null);
            Runner.Expect(CommonProperties.To,null);
            Runner.Expect(CommonProperties.Subject,null);
            Runner.Expect(CommonProperties.DataTemplate,null);
        }
        
        public async Task<XchangeFile> Handle(XchangeFile xchangeFile)
        {
            var client = new SendGridClient( Runner.StartupValueOf<string>(CommonProperties.ApiKey));
            
            var inputModel = Runner.StartupValueOf<string>(CommonProperties.InputModel);

            var msg = new SendGridMessage()
            {
                TrackingSettings = new TrackingSettings
                {
                    ClickTracking = new ClickTracking {Enable = false}
                }
            };

            if (inputModel == "EmailRequest")
            {
                var request = JsonConvert.DeserializeObject<EmailRequest>(xchangeFile.Data);

                msg.From = new EmailAddress(request.From);
                msg.Subject = request.Subject;
                msg.AddTo(new EmailAddress(request.To));
                
                if (request.Template != null)
                {
                    msg.SetTemplateId(request.Template);
                    msg.SetTemplateData(request.TemplateVariables);
                }
                else if (request.Body != null)
                {
                    msg.HtmlContent = request.Body;
                    msg.PlainTextContent = request.Body;
                }
                else
                {
                    throw new Exception("Both Body and Template can not be null.");
                }
                
                if (request.AttachmentLocations != null)
                {
                    var attachments = new List<Attachment>();
                    foreach (var (key, value) in request.AttachmentLocations)
                    {
                        var fileBytes = await GetFileBytes(value);
                        attachments.Add(new Attachment
                        {
                            Content = Convert.ToBase64String(fileBytes,0,fileBytes.Length), 
                            Filename = key,
                        });
                    }

                    msg.Attachments = attachments;
                }
                
            }
            else
            {
                msg.From = new EmailAddress(Runner.StartupValueOf<string>(CommonProperties.From));
                msg.Subject = Runner.StartupValueOf<string>(CommonProperties.Subject);
                msg.AddTo(new EmailAddress(Runner.StartupValueOf<string>(CommonProperties.To)));

                var template = Runner.StartupValueOf<string>(CommonProperties.DataTemplate);
                
                if (template != null)
                {
                    msg.SetTemplateId(template);
                    msg.SetTemplateData(xchangeFile.Data);
                }
                else
                {
                    msg.HtmlContent = xchangeFile.Data;
                    msg.PlainTextContent = xchangeFile.Data;
                }
                
            }
            
            var response = await client.SendEmailAsync(msg);

            if (response.StatusCode < HttpStatusCode.BadRequest) return new XchangeFile(response.StatusCode.ToString());

            throw new Exception(response.StatusCode.ToString());
            
        }
        
        
        public async Task<byte[]> GetFileBytes(string url)
        {
            byte[] bytes = Array.Empty<byte>();
            if (url == null || url.Substring(0, 4) != "http") return bytes;
            using (var client = new WebClient())
            {
                try
                {
                    bytes = client.DownloadData(url);
                }catch
                {
                    
                }
                
            }
            return bytes;
        }
    }
}