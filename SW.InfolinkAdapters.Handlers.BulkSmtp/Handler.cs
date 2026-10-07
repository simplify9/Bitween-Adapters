using Newtonsoft.Json;
using SW.PrimitiveTypes;
using SW.Serverless.Sdk;

namespace SW.InfolinkAdapters.Handlers.BulkSmtp;

public class Handler : IInfolinkHandler
{
    public Handler()
    {
        Runner.Expect(CommonProperties.Port);
        Runner.Expect(CommonProperties.Host);
        Runner.Expect(CommonProperties.From);
        Runner.Expect(CommonProperties.Password, false, true);
    }

    public Task<XchangeFile> Handle(XchangeFile xchangeFile)
    {
        var emailModels = JsonConvert.DeserializeObject<List<InputModel>>(xchangeFile.Data);


        foreach (var emailModel in emailModels)
        {
            Mailer.SendEmail(
                Runner.StartupValueOf(CommonProperties.Host),
                Convert.ToInt32(Runner.StartupValueOf(CommonProperties.Port)),
                Runner.StartupValueOf(CommonProperties.From),
                Runner.StartupValueOf(CommonProperties.Password),
                emailModel.To,
                emailModel.Subject,
                emailModel.Body,
                emailModel.IsHtml,
                emailModel.AttachmentName,
                emailModel.AttachmentBody
            );
        }

        return Task.FromResult(new XchangeFile(string.Empty));
    }
}