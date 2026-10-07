using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SendGrid;
using SendGrid.Helpers.Mail;
using SW.InfolinkAdapters;
using SW.PrimitiveTypes;
using SW.Serverless.Sdk;

namespace SW.Infolink.Adapters.Handlers.Notifier.Sendgrid
{
    public class Handler: IInfolinkHandler
    {
        public Handler()
        {
            Runner.Expect(CommonProperties.ApiKey, false, true);
            Runner.Expect(CommonProperties.From);
            Runner.Expect(CommonProperties.To);
            Runner.Expect(CommonProperties.Subject);

        }
        
        public async Task<XchangeFile> Handle(XchangeFile xchangeFile)
        {
            var model = JsonConvert.DeserializeObject<NotificationModel>(xchangeFile.Data);
            
            var client = new SendGridClient( Runner.StartupValueOf<string>(CommonProperties.ApiKey));
            
            var msg = new SendGridMessage()
            {
                From = new EmailAddress(Runner.StartupValueOf<string>(CommonProperties.From)),
                Subject = Runner.StartupValueOf<string>(CommonProperties.Subject),
                TrackingSettings = new TrackingSettings
                {
                    ClickTracking = new ClickTracking { Enable = false }
                },
            };
            if (Runner.StartupValueOf<string>(CommonProperties.To).Contains(','))
            {
                var tos = Runner.StartupValueOf<string>(CommonProperties.To).Split(',')
                    .Select(e => new EmailAddress(e)).ToList();

                msg.AddTos(tos);
            }
            else
            {
                msg.AddTo(new EmailAddress(Runner.StartupValueOf<string>(CommonProperties.To)));
            }

            var status = model.Success ? "Succeeded" : "Failed";
            
            msg.HtmlContent = @$"<h1>Xchange Number {model.Id}</h1>
               <span><strong>ID</strong>{model.Id}</span>
               <span><strong>Status</strong>{status}</span>
               <span><strong>Finished On</strong>{model.FinishedOn}</span>
               <span><strong>Bad Output</strong>{model.OutputBad}</span>
                <span><strong>Bad Output</strong>{model.ResponseBad}</span>
                <span><strong>Exception</strong>{model.Exception}</span>
            ";
            
            
            
            
            var response = await client.SendEmailAsync(msg);

            if (response.StatusCode < HttpStatusCode.BadRequest) return new XchangeFile(response.StatusCode.ToString());

            throw new Exception(response.StatusCode.ToString());
        }
    }
}