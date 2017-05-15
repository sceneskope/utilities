using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Newtonsoft.Json;
using Polly;
using SceneSkope.Utilities.Text;
using Serilog;

namespace SceneSkope.Utilities.EventLogs
{
    internal class EventLogFiles : ILogFiles
    {
        private readonly string _connectionString;
        private readonly string _partitionName;
        private readonly string _consumerGroup;
        private readonly Policy _retryPolicy;
        private readonly long _epoch = DateTime.UtcNow.Ticks;

        private EventHubClient _client;
        private PartitionReceiver _receiver;

        public LogFilesStatus Status { get; }

        public EventLogFiles(string connectionString, string partitionName, string consumerGroup, LogFilesStatus status)
        {
            _connectionString = connectionString;
            _partitionName = partitionName;
            _consumerGroup = consumerGroup;
            Status = status ?? new LogFilesStatus { Pattern = "" };
            _retryPolicy = Policy
                .Handle<Exception>(ex =>
                {
                    switch (ex)
                    {
                        case EventHubsException _: return true;
                        case UnauthorizedAccessException _: return true;
                        case TimeoutException _: return true;
                        case SocketException _: return true;
                        default:
                            Log.Warning("Caught {exception} ({type}), but not handling", ex.Message, ex.GetType());
                            return false;
                    }
                })
                .WaitAndRetryForeverAsync(_ => TimeSpan.FromSeconds(10),
                (ex, ts) =>
                {
                    Log.Information("Caught {exception}, delaying for {ts}", ex.Message, ts);
                    return CloseClientIfRequiredAsync();
                });
        }

        private async Task CloseClientIfRequiredAsync()
        {
            try
            {
                if (_receiver != null)
                {
                    await _receiver.CloseAsync().ConfigureAwait(false);
                    _receiver = null;
                    await _client.CloseAsync().ConfigureAwait(false);
                    _client = null;
                    Log.Information("Receiver and client closed");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error closing: {exception}", ex.Message);
                throw;
            }
        }

        private void CloseClientIfRequired()
        {
            if (_receiver != null)
            {
                _receiver.Close();
                _receiver = null;
                _client.Close();
                _client = null;
            }
        }

        private PartitionReceiver GetOrCreateReceiver()
        {
            var receiver = _receiver;
            if (receiver == null)
            {
                var client = EventHubClient.CreateFromConnectionString(_connectionString);
                client.RetryPolicy = RetryPolicy.NoRetry;
                _client = client;

                receiver = client.CreateEpochReceiver(_consumerGroup, _partitionName, Status.Offset ?? PartitionReceiver.StartOfStream, Status.Offset == null, _epoch);
                receiver.RetryPolicy = RetryPolicy.NoRetry;
                _receiver = receiver;
            }
            return receiver;
        }

        public void Dispose() => CloseClientIfRequired();

        private IList<EventData> _eventCache;
        private int _eventIndex;

        public Task<UploadedLine> TryReadNextLineAsync(CancellationToken cancel)
        {
            if (_eventCache != null)
            {
                return ReadNextFromEventCache();
            }
            else
            {
                return PopulateAndReadFromEventCacheAsync(cancel);
            }
        }

        private Task<UploadedLine> ReadNextFromEventCache()
        {
            var eventData = _eventCache[_eventIndex++];
            if (_eventIndex == _eventCache.Count)
            {
                _eventCache = null;
            }

            var segment = eventData.Body;
            var json = Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count);
            var line = JsonConvert.DeserializeObject<UploadedLine>(json);
            Status.LineNumber = line.LineNumber;
            Status.Position = line.Position;
            Status.CurrentName = line.FileName;
            Status.Offset = eventData.SystemProperties.Offset;
            return Task.FromResult(line);
        }

        private async Task<UploadedLine> PopulateAndReadFromEventCacheAsync(CancellationToken ct)
        {
            if (!(await TryPopulateCacheAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false)))
            {
                return null;
            }
            else
            {
                return await ReadNextFromEventCache().ConfigureAwait(false);
            }
        }

        private async Task<bool> TryPopulateCacheAsync(TimeSpan duration, CancellationToken ct)
        {
            using (ct.Register(() => CloseClientIfRequired()))
            {
                var events = (await ReceiveAsync(duration, ct).ConfigureAwait(false)) as IList<EventData>;
                var count = events?.Count ?? 0;
                if (count > 0)
                {
                    _eventCache = events;
                    _eventIndex = 0;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private Task<IEnumerable<EventData>> ReceiveAsync(TimeSpan duration, CancellationToken ct) =>
            _retryPolicy.ExecuteAsync(_ => GetOrCreateReceiver().ReceiveAsync(100, duration), ct, false);
    }
}
