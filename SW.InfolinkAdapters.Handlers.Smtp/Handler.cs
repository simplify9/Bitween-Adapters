using Newtonsoft.Json;
using SW.PrimitiveTypes;
using SW.Serverless.Sdk;

namespace SW.InfolinkAdapters.Handlers.Smtp;

public class Handler : IInfolinkHandler
{
    public Handler()
    {
        Runner.Expect(CommonProperties.Port);
        Runner.Expect(CommonProperties.Host);
        Runner.Expect(CommonProperties.From);
        Runner.Expect(CommonProperties.Password, null, true);
        Runner.Expect(CommonProperties.To, null);
        Runner.Expect("OtherTo", null);
        Runner.Expect("Cc", null);
        Runner.Expect("Bcc", null);
    }

    private static List<string> GetMailList(List<string>? mails, string startupValueKey)
    {
        var emails = new List<string>();

        if (mails is not null)
        {
            emails.AddRange(mails);
        }

        var value = Runner.StartupValueOf(startupValueKey);
        if (!string.IsNullOrEmpty(value))
        {
            emails.AddRange(value.Split(",").Select(i => i.Trim()));
        }

        return emails;
    }

    public Task<XchangeFile> Handle(XchangeFile xchangeFile)
    {
        var emailModel = JsonConvert.DeserializeObject<InputModel>(xchangeFile.Data);

        var startupEmail = Runner.StartupValueOf(CommonProperties.To);
        var emailTo = string.IsNullOrEmpty(startupEmail) ? emailModel.To : startupEmail;
        var toList = Runner.StartupValueOf("OtherTo");

        if (!string.IsNullOrWhiteSpace(toList))
        {
            emailModel.OtherTo ??= new List<string>();
            emailModel.OtherTo.AddRange(toList.Split(",").Select(i => i.Trim()));
        }

        Mailer.SendEmail(
            Runner.StartupValueOf(CommonProperties.Host),
            Convert.ToInt32(Runner.StartupValueOf(CommonProperties.Port)),
            Runner.StartupValueOf(CommonProperties.From),
            Runner.StartupValueOf(CommonProperties.Password),
            emailTo,
            emailModel.OtherTo,
            GetMailList(emailModel.Cc, "Cc"),
            GetMailList(emailModel.Bcc, "Bcc"),
            emailModel.Subject,
            emailModel.Body,
            emailModel.IsHtml,
            emailModel.AttachmentName,
            emailModel.AttachmentBody,
            false
        );
        return Task.FromResult(new XchangeFile(string.Empty));
    }
}