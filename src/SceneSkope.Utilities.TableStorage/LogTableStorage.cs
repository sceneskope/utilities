using System;
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
    public class LogTableStorage : BaseLogDirectory
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

        public static async Task<ILogDirectory> CreateAsync(string account, string accessKey, string tableName, string statusBlobContainerName, string statusBlobName, CancellationToken ct)
        {
            var credentials = new StorageCredentials(account, accessKey);
            var storageAccount = new CloudStorageAccount(credentials, true);
            var table = await CreateTableStorageAsync(storageAccount, tableName, ct).ConfigureAwait(false);
            var blob = await CreateStatusBlobAsync(storageAccount, statusBlobContainerName, statusBlobName, ct).ConfigureAwait(false);
            var status = new LogBlobStatus(blob);

            var storage = new LogTableStorage(table, status);
            await storage.InitialiseAsync(ct).ConfigureAwait(false);
            return storage;
        }

#pragma warning disable RCS1163 // Unused parameter.
        private static async Task<CloudTable> CreateTableStorageAsync(CloudStorageAccount account, string tableName, CancellationToken ct)
#pragma warning restore RCS1163 // Unused parameter.
        {
            var client = account.CreateCloudTableClient();
            var table = client.GetTableReference(tableName);
            if (!(await table.ExistsAsync().ConfigureAwait(false)))
            {
                await table.CreateAsync().ConfigureAwait(false);
            }
            return table;
        }

        private static async Task<CloudBlockBlob> CreateStatusBlobAsync(CloudStorageAccount account, string blobContainer, string blobName, CancellationToken ct)
        {
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(blobContainer);
            await _policy.ExecuteAsync(cancel =>
                container.CreateIfNotExistsAsync(), ct, false).ConfigureAwait(false);

            var blob = container.GetBlockBlobReference(blobName);
            return blob;
        }

        private readonly CloudTable _table;

        public LogTableStorage(CloudTable table, LogBlobStatus statusBlob) : base(statusBlob)
        {
            _table = table;
        }

        public override void Dispose()
        {
        }

        public override Task<ILogFiles> GetLogFilesAsync(string pattern, CancellationToken ct)
        {
            var status = GetOrCreateStatusForPattern(pattern);
            var partitions = new LogTablePartitions(_table, pattern, status);
            return Task.FromResult((ILogFiles)partitions);
        }
    }
}
