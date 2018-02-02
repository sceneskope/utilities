using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using SceneSkope.Utilities.Text;

namespace SceneSkope.Utilities.BlobStorage
{
    public class LogBlobStorage : BaseLogDirectory
    {
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
