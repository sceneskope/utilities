using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using SceneSkope.Utilities.Text;

namespace SceneSkope.Utilities.EventLogs
{
    public class EventLogStorage : ILogDirectory
    {
        public static async Task<ILogDirectory> CreateAsync(string host, string keyName, string keyValue, string eventHubName, string partitionName, string consumerGroup, ILogStatus status, CancellationToken ct)
        {
            await status.InitialiseAsync(ct).ConfigureAwait(false);
            return new EventLogStorage(host, keyName, keyValue, eventHubName, partitionName, consumerGroup, status);
        }

        private readonly string _connectionString;
        private readonly string _partitionName;
        private readonly string _consumerGroup;
        private readonly ILogStatus _status;

        public EventLogStorage(string host, string keyName, string keyValue, string eventHubName, string partitionName, string consumerGroup, ILogStatus status)
        {
            var builder = new EventHubsConnectionStringBuilder(new Uri($"amqps://{host}"), eventHubName, keyName, keyValue);
            _connectionString = builder.ToString();
            _partitionName = partitionName;
            _consumerGroup = consumerGroup;
            _status = status;
        }

        public Task<ILogFiles> GetLogFilesAsync(string pattern, CancellationToken ct)
        {
            var status = _status.GetOrCreateStatusForPattern(pattern);
            var logFiles = new EventLogFiles(_connectionString, _partitionName, _consumerGroup, status);
            return Task.FromResult<ILogFiles>(logFiles);
        }

        public Task SaveStatusAsync(CancellationToken ct) => _status.SaveStatusAsync(ct);

        public void Dispose()
        {
        }
    }
}
