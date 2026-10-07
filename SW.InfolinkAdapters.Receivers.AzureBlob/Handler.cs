using SW.Serverless.Sdk;
using System;
using System.Threading.Tasks;
using SW.PrimitiveTypes;
using Azure.Storage.Blobs;
using System.Text;
using System.IO;
using System.Collections.Generic;
using Azure.Storage.Blobs.Models;

namespace SW.InfolinkAdapters.Receivers.AzureBlob
{
    public class Handler : IInfolinkReceiver
    {
        BlobContainerClient container;
        public Handler()
        {
            Runner.Expect(CommonProperties.ConnectionString, false, true);
            Runner.Expect(CommonProperties.TargetPath);
        }

        public async Task DeleteFile(string fileId)
        {
            var _blockBlob = container.GetBlobClient(fileId);
            await _blockBlob.DeleteAsync();
            return;
        }

        public async Task Finalize()
        {
            return;
        }

        public async Task<XchangeFile> GetFile(string fileId)
        {
            var blockBlob = container.GetBlobClient(fileId);
            var data = await blockBlob.DownloadAsync();
            using StreamReader reader = new StreamReader(data.Value.Content);
            return new XchangeFile(reader.ReadToEnd(), fileId);
        }

        public async Task Initialize()
        {
            container = new BlobContainerClient(Runner.StartupValueOf(CommonProperties.ConnectionString), Runner.StartupValueOf(CommonProperties.TargetPath));

            if (container == null)
                throw new Exception("Container not found");

        }

        public async Task<IEnumerable<string>> ListFiles()
        {
            List<string> blobNames = new List<string>();
            await foreach (BlobItem blob in container.GetBlobsAsync())
            {
                blobNames.Add(blob.Name);
            }
            return blobNames;
        }

    }
}
