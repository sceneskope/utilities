using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Polly;
using SceneSkope.Utilities.Text;
using Serilog;

namespace SceneSkope.Utilities.BlobStorage
{
    public class LogBlobStorage : BaseLogDirectory
    {
        private static readonly Policy _policy =
            Policy
            .Handle<StorageException>(ex =>
            {
                switch (ex?.RequestInformation?.HttpStatusCode)
                {
                    default: return true;
                }
            })
            .WaitAndRetryForeverAsync(attempt => TimeSpan.FromSeconds(2), (ex, ts)
                => Log.Warning("Delaying {delay} due to {exception}", ts, ex.Message));

        public static async Task<ILogDirectory> CreateAsync(CloudStorageAccount account, string containerName, ILogStatus status, CancellationToken ct)
        {
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Container, null, null, ct).ConfigureAwait(false);
            var storage = new LogBlobStorage(container, status);
            await storage.InitialiseAsync(ct).ConfigureAwait(false);
            return storage;
        }

        private readonly CloudBlobContainer _container;

        private LogBlobStorage(CloudBlobContainer container, ILogStatus status) : base(status)
        {
            _container = container;
        }

        public override void Dispose()
        {
        }

        public override Task<ILogFiles> GetLogFilesAsync(string pattern, CancellationToken ct)
        {
            var status = GetOrCreateStatusForPattern(pattern);
            var blobs = new LogBlobs(_container, pattern, status);
            return Task.FromResult((ILogFiles)blobs);
        }
    }
}
