using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using SceneSkope.Utilities.Text;

namespace SceneSkope.Utilities.TableStorage
{
    internal sealed class LogTablePartitions : BaseLogFiles<LogTablePartition>
    {
        private readonly CloudTable _table;
        private string _highestName;
        private readonly Regex _patternRegex;
        private bool _firstTime = true;

        public LogTablePartitions(CloudTable table, string pattern, LogFilesStatus status) : base(pattern, status)
        {
            _table = table;
            _patternRegex = BaseLogDirectory.CreatePatternRegex(pattern);
        }

        protected override async Task<IEnumerable<string>> FindNewFilesAsync(CancellationToken ct)
        {
            _highestName = _highestName ?? Status?.CurrentName ?? "";

            string newHighest = null;
            TableContinuationToken continuation = null;
            List<string> partitions = null;
            var query = new TableQuery<LogTableEntity>().Where(
                TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition(nameof(LogTableEntity.PartitionKey), QueryComparisons.GreaterThanOrEqual, _highestName),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition(nameof(LogTableEntity.RowKey), QueryComparisons.Equal, LogTableEntity.FormatRowKey(1))
                    )
                )
                .Select(new List<string> { nameof(LogTableEntity.PartitionKey) });

            do
            {
                var results = await _table.ExecuteQuerySegmentedAsync(query, continuation, null, null, ct).ConfigureAwait(false);
                foreach (var entity in results.Results)
                {
                    var key = entity.PartitionKey;
                    if (_patternRegex.IsMatch(key)
                        && (string.Compare(key, _highestName, StringComparison.OrdinalIgnoreCase) > (_firstTime ? -1 : 0)))
                    {
                        partitions = partitions ?? new List<string>();
                        partitions.Add(key);
                        newHighest = (newHighest == null)
                            ? key : string.Compare(key, newHighest, StringComparison.OrdinalIgnoreCase) > 0
                                ? key
                                : newHighest;
                    }
                }
                continuation = results.ContinuationToken;

            } while (!ct.IsCancellationRequested && (continuation != null));
            if (newHighest != null)
            {
                _firstTime = false;
                _highestName = newHighest;
            }
            return partitions;
        }

        protected override Task<LogTablePartition> GetLogFileAsync(string name, CancellationToken ct, long? position = default, int? lineNumber = default)
        {
            var partition = new LogTablePartition(_table, name, lineNumber);
            return Task.FromResult(partition);
        }
    }
}
