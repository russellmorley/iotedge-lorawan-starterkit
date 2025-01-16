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

namespace LoRaTools
{
    public interface IBlobStorageManager
    {
        public Task<(long, Stream)> GetBlobStreamAsync(string blobUrl, CancellationToken cancellationToken);
        public Task<string> GetBase64EncodedBlobAsync(string blobUrl, CancellationToken cancellationToken);
    }
}
