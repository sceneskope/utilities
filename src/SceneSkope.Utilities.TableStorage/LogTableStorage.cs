using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using SceneSkope.Utilities.Text;

namespace SceneSkope.Utilities.TableStorage
{
    public class LogTableStorage : BaseLogDirectory
    {
        public static async Task<ILogDirectory> CreateAsync(CloudStorageAccount account, string tableName, ILogStatus status, CancellationToken ct)
        {
            var table = CreateTableStorage(account, tableName);

            var storage = new LogTableStorage(table, status);
            await storage.InitialiseAsync(ct).ConfigureAwait(false);
            return storage;
        }

        private static CloudTable CreateTableStorage(CloudStorageAccount account, string tableName)
        {
            var client = account.CreateCloudTableClient();
            return client.GetTableReference(tableName);
        }

        private readonly CloudTable _table;

        public LogTableStorage(CloudTable table, ILogStatus status) : base(status)
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
