using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using Polly;
using SceneSkope.Utilities.Text;
using Serilog;

namespace SceneSkope.Utilities.TableStorage
{
    public class LogBlobStatus<T> : ILogStatus<T> where T : LogFilesStatus
    {
        private readonly JsonSerializerSettings _settings;

        private readonly CloudBlockBlob _blob;
        private List<T> _statuses;
        private readonly Func<string, T> _creator;
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

        public LogBlobStatus(CloudBlockBlob blob, Func<string, T> creator)
        {
            _blob = blob;
            _creator = creator;
            _settings = new JsonSerializerSettings
            {
                ContractResolver = new DictionaryAsArrayResolver(),
                Formatting = Formatting.Indented
            }.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        }

        public Task SaveStatusAsync(CancellationToken ct)
        {
            var json = JsonConvert.SerializeObject(_statuses, _settings);
            return _policy.ExecuteAsync(cancel =>
                _blob.UploadTextAsync(json, Encoding.UTF8, null, null, null, cancel), ct, false);
        }

        public T GetOrCreateStatusForPattern(string pattern)
        {
            if (_statuses == null)
            {
                throw new InvalidOperationException("Not yet initialised");
            }
            var status = _statuses.Find(s => s.Pattern.Equals(pattern));
            if (status == null)
            {
                status = _creator(pattern);
                _statuses.Add(status);
            }
            return status;
        }

        public async Task InitialiseAsync(CancellationToken ct)
        {
            if (await _policy.ExecuteAsync(cancel => _blob.ExistsAsync(null, null, cancel), ct, false).ConfigureAwait(false))
            {
                try
                {
                    var json = await _policy.ExecuteAsync(cancel =>
                        _blob.DownloadTextAsync(Encoding.UTF8, null, null, null, cancel), ct, false).ConfigureAwait(false);
                    _statuses = JsonConvert.DeserializeObject<List<T>>(json, _settings);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to read status files: {exception}", ex.Message);
                    _statuses = new List<T>();
                }
            }
            else
            {
                _statuses = new List<T>();
            }
        }
    }
}
