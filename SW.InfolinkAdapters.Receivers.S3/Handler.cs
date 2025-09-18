using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SW.CloudFiles;
using SW.PrimitiveTypes;
using SW.Serverless.Sdk;

namespace SW.InfolinkAdapters.Receivers.S3
{
    public class Handler: IInfolinkReceiver
    {
        private CloudFilesService cloudFiles;
        public Handler()
        {
            Runner.Expect(CommonProperties.AccessKeyId);
            Runner.Expect(CommonProperties.SecretAccessKey);
            Runner.Expect(CommonProperties.Url);
            Runner.Expect(CommonProperties.TargetPath);
            Runner.Expect(CommonProperties.FolderName, "");
            Runner.Expect(CommonProperties.BatchSize, "50");
            Runner.Expect(CommonProperties.ResponseEncoding, "utf8");
            Runner.Expect(CommonProperties.DeleteMovesFileTo, null);
        }
        public async Task Initialize()
        {
            cloudFiles = new CloudFilesService(new CloudFilesOptions
            {
                AccessKeyId = Runner.StartupValueOf(CommonProperties.AccessKeyId),
                SecretAccessKey = Runner.StartupValueOf(CommonProperties.SecretAccessKey),
                ServiceUrl = Runner.StartupValueOf(CommonProperties.Url),
                BucketName = Runner.StartupValueOf(CommonProperties.TargetPath),
            });
        }

        public async Task Finalize()
        {
        }

        public async Task<IEnumerable<string>> ListFiles()
        {
            var key = Runner.StartupValueOf(CommonProperties.FolderName);
            var files = await cloudFiles.ListAsync(key);
            var batchSize = Convert.ToInt32(Runner.StartupValueOf(CommonProperties.BatchSize));
            var filesList = files.Where(f => !f.Key.EndsWith("/")).Select(f => f.Key).Take(batchSize).ToArray();
            
            return filesList;
        }

        public async Task<XchangeFile> GetFile(string fileId)
        {
            var file = await cloudFiles.OpenReadAsync(fileId);
            var fileName = Path.GetFileName(fileId);
            byte[] bytes;
            await using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                bytes = memoryStream.ToArray();
            }
            string base64 = Convert.ToBase64String(bytes);
            XchangeFile xchangeFile = Runner.StartupValueOf("ResponseEncoding").ToLower() switch
            {
                "utf8" => new XchangeFile(Encoding.UTF8.GetString(bytes), fileName),
                "base64" => new XchangeFile(base64, fileName),
                _ => throw new ArgumentException($"Unknown ResponseEncoding: {Runner.StartupValueOf("ResponseEncoding")}. Supported: utf8, base64")
            };

            return xchangeFile;
        }

        public async Task DeleteFile(string fileId)
        {
            if (Runner.StartupValueOf(CommonProperties.DeleteMovesFileTo) != null)
            {
                var file = await cloudFiles.OpenReadAsync(fileId);
                var metadata = await cloudFiles.GetMetadataAsync(fileId);
                var contentType = metadata["ContentType"];
                var fileName = Path.GetFileName(fileId);
                var key = $"{Runner.StartupValueOf(CommonProperties.DeleteMovesFileTo)}/{fileName}";
                await using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    await cloudFiles.WriteAsync(memoryStream, new WriteFileSettings
                    {
                        Key = key,
                        ContentType = contentType,
                        Public = false
                    });
                }
                await cloudFiles.DeleteAsync(fileId);
            }
        }
    }
}