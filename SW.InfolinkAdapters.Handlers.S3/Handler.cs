using SW.CloudFiles;
using SW.PrimitiveTypes;
using SW.Serverless.Sdk;
using System;
using System.Threading.Tasks;

namespace SW.InfolinkAdapters.Handlers.S3
{
  public  class Handler : IInfolinkHandler 
    {
        public Handler()
        {
            Runner.Expect(CommonProperties.AccessKeyId);
            Runner.Expect(CommonProperties.SecretAccessKey, false, true);
            Runner.Expect(CommonProperties.Url);
            Runner.Expect(CommonProperties.TargetPath);
            Runner.Expect(CommonProperties.FolderName);
            Runner.Expect(CommonProperties.FileName, "");
            Runner.Expect(CommonProperties.FileExtension, "");
            Runner.Expect(CommonProperties.ContentType, "text/plain");
        }

        public async Task<XchangeFile> Handle(XchangeFile xchangeFile)
        {
            using var cloudFiles = new CloudFilesService(new CloudFilesOptions
            {

                AccessKeyId = Runner.StartupValueOf(CommonProperties.AccessKeyId),
                SecretAccessKey = Runner.StartupValueOf(CommonProperties.SecretAccessKey),
                ServiceUrl = Runner.StartupValueOf(CommonProperties.Url),
                BucketName = Runner.StartupValueOf(CommonProperties.TargetPath),

            });

            var key = "";
            if (Runner.StartupValueOf(CommonProperties.FileName) != "")
                key = Runner.StartupValueOf("FileName");
            else
                key = string.Concat(Runner.StartupValueOf(CommonProperties.FolderName), "/", DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "." + Runner.StartupValueOf(CommonProperties.FileExtension));

            await cloudFiles.WriteTextAsync(xchangeFile.Data, new WriteFileSettings
            {
                Key = key,
                ContentType = Runner.StartupValueOf(CommonProperties.ContentType),
            }) ;

            return new XchangeFile(key, xchangeFile.Filename ); 
        }
    }
}
