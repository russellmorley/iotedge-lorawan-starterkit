using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LoRaTools.IoTHubImpl;
using Microsoft.Azure.Devices;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace LoRaTools.AzureBlobStorage
{
    public class AzureBlobStorageManager : IBlobStorageManager
    {
        private readonly BlobServiceClient blobServiceClient;
        private readonly ILogger logger;

        public static IBlobStorageManager CreateWithProvider(
            Func<BlobServiceClient> blobServiceClientProvider,
            ILogger logger)
            {
                return blobServiceClientProvider == null
                    ? throw new ArgumentNullException(nameof(blobServiceClientProvider))
                    : (IBlobStorageManager)new AzureBlobStorageManager(blobServiceClientProvider, logger);
            }

        internal AzureBlobStorageManager(Func<BlobServiceClient> blobServiceClientProvider, ILogger logger)
        {
            this.blobServiceClient = blobServiceClientProvider() ?? throw new InvalidOperationException("BlobServiceClientProvider provider provided a null blobServiceClient.");
            this.logger = logger;
        }
        public async Task<(long, Stream)> GetBlobStreamAsync(string blobUrl, CancellationToken cancellationToken)
        {
            var blobUri = new BlobUriBuilder(new Uri(blobUrl));
            var blobClient = blobServiceClient.GetBlobContainerClient(blobUri.BlobContainerName)
                                              .GetBlobClient(blobUri.BlobName);
            var blobProperties = await blobClient.GetPropertiesAsync(null, cancellationToken);
            var streamingResult = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return (blobProperties.Value.ContentLength, streamingResult.Value.Content);
        }

        public async Task<string> GetBase64EncodedBlobAsync(string blobUrl, CancellationToken cancellationToken)
        {
            var blobUri = new BlobUriBuilder(new Uri(blobUrl));
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(blobUri.BlobContainerName);
            var blobClient = blobContainerClient.GetBlobClient(blobUri.BlobName);
            using var blobStream = await blobClient.OpenReadAsync(new BlobOpenReadOptions(false), cancellationToken);
            //using var blobStream = await blobServiceClient.GetBlobContainerClient(blobUri.BlobContainerName)
            //    .GetBlobClient(blobUri.BlobName)
            //    .OpenReadAsync(new BlobOpenReadOptions(false), cancellationToken);
            using var base64transform = new ToBase64Transform();
            using var base64Stream = new CryptoStream(blobStream, base64transform, CryptoStreamMode.Read);
            using var memoryStream = new MemoryStream();
            using var reader = new StreamReader(memoryStream);
            await base64Stream.CopyToAsync(memoryStream, cancellationToken);
            _ = memoryStream.Seek(0, SeekOrigin.Begin);
            return await reader.ReadToEndAsync();
        }
    }
}
