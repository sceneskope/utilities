using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Polly;
using SceneSkope.Utilities.Text;
using Serilog;

namespace SceneSkope.Utilities.TableStorage
{
    internal class LogTablePartition : ILogFile
    {
        private readonly CloudTable _table;
        private List<LogTableEntity> _buffer;
        private int _bufferPosition;
        private string _rowKey;
        private readonly Policy _policy =
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

        public LogTablePartition(CloudTable table, string name, int? lineNumber)
        {
            _table = table;
            Name = name;
            _rowKey = LogTableEntity.FormatRowKey(lineNumber ?? 0);
        }

        public string Name { get; }

        public long Position => 0;

        public void Dispose()
        {
        }

        public async Task<(string line, int lineNumber)> TryReadNextLineAsync(CancellationToken ct)
        {
            _buffer = _buffer ?? new List<LogTableEntity>();
            if (_bufferPosition == _buffer.Count)
            {
                await FillBufferAsync(ct).ConfigureAwait(false);
                if (_bufferPosition == _buffer.Count)
                {
                    return (null, -1);
                }
            }

            var line = _buffer[_bufferPosition++];
            var str = LogCompressor.Decompress(line.Data);
            _rowKey = line.RowKey;
            return (str, LogTableEntity.ParseRoadKey(line.RowKey));
        }

        private async Task FillBufferAsync(CancellationToken ct)
        {
            var query = new TableQuery<LogTableEntity>().Where(
               TableQuery.CombineFilters(
                   TableQuery.GenerateFilterCondition(nameof(LogTableEntity.PartitionKey), QueryComparisons.Equal, Name),
                   TableOperators.And,
                   TableQuery.GenerateFilterCondition(nameof(LogTableEntity.RowKey), QueryComparisons.GreaterThan, _rowKey)
                   )
               );

            var results = await _table.ExecuteQuerySegmentedAsync(query, null, null, null, ct).ConfigureAwait(false);
            _buffer = results.Results;
            _bufferPosition = 0;
        }
    }
}
