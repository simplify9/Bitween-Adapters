using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SW.PrimitiveTypes;
using SW.Serverless.Sdk;
using Rebex.Net;


namespace SW.InfolinkAdapters.Receivers.Pop3
{
    public class Handler: IInfolinkReceiver
    {
        private Rebex.Net.Pop3 _pop3;
        public Handler()
        {
            Runner.Expect(CommonProperties.LicenseKey, false, true);
            Runner.Expect(CommonProperties.Host);
            Runner.Expect(CommonProperties.Username);
            Runner.Expect(CommonProperties.Password, false, true);
            Runner.Expect(CommonProperties.BatchSize, "50");
            Runner.Expect(CommonProperties.ResponseEncoding, "utf8");
        }
        public async Task Initialize()
        {
            Rebex.Licensing.Key = Runner.StartupValueOf(CommonProperties.LicenseKey);
            _pop3 = new Rebex.Net.Pop3();
            await _pop3.ConnectAsync(Runner.StartupValueOf(CommonProperties.Host), SslMode.Implicit);
            await _pop3.LoginAsync(Runner.StartupValueOf(CommonProperties.Username),
                Runner.StartupValueOf(CommonProperties.Password));
        }

        public async Task Finalize()
        {
            await _pop3.DisconnectAsync(false);
            _pop3.Dispose();
        }

        public async Task<IEnumerable<string>> ListFiles()
        {
            var messages = await _pop3.GetMessageListAsync(Pop3ListFields.Fast);

            var batchSize = Convert.ToInt32(Runner.StartupValueOf(CommonProperties.BatchSize));
            
            return messages.Select(m => m.SequenceNumber.ToString())
                .Take(batchSize)
                .ToList();
        }

        public async Task<XchangeFile> GetFile(string fileId)
        {
            if (!int.TryParse(fileId, out var sequenceNumber)){}
            
            var message = await _pop3.GetMailMessageAsync(sequenceNumber);

            if (message.Attachments.Count < 1) return new XchangeFile(message.BodyText, message.Subject);
            var attachment = message.Attachments[0];

            await _pop3.GetMessageAsync(sequenceNumber, attachment.FileName);

            var stream = attachment.GetContentStream();

            var buffer = new byte[stream.Length + 10];

            var numBytesToRead = (int)stream.Length;
            var numBytesRead = 0;
            do
            {
                var n = await stream.ReadAsync(buffer, numBytesRead, 10);
                numBytesRead += n;
                numBytesToRead -= n;
            } while (numBytesToRead > 0);
            stream.Close();

            return (Runner.StartupValueOf(CommonProperties.ResponseEncoding).ToLower()) switch
            {
                "base64" => new XchangeFile(Convert.ToBase64String(buffer), message.Subject),
                "utf8" => new XchangeFile(Encoding.UTF8.GetString(buffer), message.Subject),
                _ => throw new ArgumentException($"Unknown {nameof(CommonProperties.ResponseEncoding)} '{Runner.StartupValueOf(CommonProperties.ResponseEncoding)}'"),
            };
            

        }

        public async Task DeleteFile(string fileId)
        {
            if (!int.TryParse(fileId, out var sequenceNumber)){}
            await _pop3.DeleteAsync(sequenceNumber);
        }
    }
}