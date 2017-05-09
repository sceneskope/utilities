using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Polly;
using SceneSkope.Utilities.Text;
using Serilog;

namespace SceneSkope.Utilities.TableStorage
{
    public class LogTableStorage<TStatus> : BaseLogDirectory<TStatus>
        where TStatus : LogFilesStatus, new()
    {
#pragma warning disable RCS1158 // Static member in generic type should use a type parameter.
        private static readonly Policy _policy =
#pragma warning restore RCS1158 // Static member in generic type should use a type parameter.
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

        public static async Task<ILogDirectory<TStatus>> CreateAsync(string account, string accessKey, string tableName, string statusBlobContainerName, string statusBlobName, Func<string, TStatus> creator, CancellationToken ct)
        {
            var credentials = new StorageCredentials(account, accessKey);
            var storageAccount = new CloudStorageAccount(credentials, true);
            var table = await CreateTableStorageAsync(storageAccount, tableName, ct).ConfigureAwait(false);
            var blob = await CreateStatusBlobAsync(storageAccount, statusBlobContainerName, statusBlobName, ct).ConfigureAwait(false);
            var status = new LogBlobStatus<TStatus>(blob, creator);

            var storage = new LogTableStorage<TStatus>(table, status);
            await storage.InitialiseAsync(ct).ConfigureAwait(false);
            return storage;
        }

#pragma warning disable RCS1158 // Static member in generic type should use a type parameter.
        private static async Task<CloudTable> CreateTableStorageAsync(CloudStorageAccount account, string tableName, CancellationToken ct)
#pragma warning restore RCS1158 // Static member in generic type should use a type parameter.
        {
            var client = account.CreateCloudTableClient();
            var table = client.GetTableReference(tableName);
            await table.CreateIfNotExistsAsync(null, null, ct).ConfigureAwait(false);
            return table;
        }

#pragma warning disable RCS1158 // Static member in generic type should use a type parameter.
        private static async Task<CloudBlockBlob> CreateStatusBlobAsync(CloudStorageAccount account, string blobContainer, string blobName, CancellationToken ct)
#pragma warning restore RCS1158 // Static member in generic type should use a type parameter.
        {
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(blobContainer);
            await _policy.ExecuteAsync(cancel =>
                container.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Off, null, null, cancel), ct, false).ConfigureAwait(false);

            var blob = container.GetBlockBlobReference(blobName);
            return blob;
        }

        private readonly CloudTable _table;

        public LogTableStorage(CloudTable table, LogBlobStatus<TStatus> statusBlob) : base(statusBlob)
        {
            _table = table;
        }

        public override void Dispose()
        {
        }

        public override Task<ILogFiles<TStatus>> GetLogFilesAsync(string pattern, CancellationToken ct)
        {
            var status = GetOrCreateStatusForPattern(pattern);
            var partitions = new LogTablePartitions<TStatus>(_table, pattern, status);
            return Task.FromResult((ILogFiles<TStatus>)partitions);
        }
    }
}
