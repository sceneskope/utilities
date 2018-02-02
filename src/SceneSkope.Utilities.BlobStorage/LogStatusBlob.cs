using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Polly;
using SceneSkope.Utilities.Text;
using Serilog;

namespace SceneSkope.Utilities.BlobStorage
{
    public class LogStatusBlob : ILogStatus
    {
        private readonly JsonSerializerSettings _settings;

        private readonly CloudBlockBlob _blob;
        private List<LogFilesStatus> _statuses;
        private readonly Policy _policy =
            Policy
            .Handle<StorageException>(ex =>
            {
                switch (ex?.RequestInformation?.HttpStatusCode)
                {
                    default: return true;
                }
            })
            .WaitAndRetryForeverAsync(_ => TimeSpan.FromSeconds(2), (ex, ts)
                => Log.Warning("Delaying {Delay} due to {Exception}", ts, ex.Message));

        public LogStatusBlob(CloudStorageAccount account, string containerName, string blobName)
        {
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(containerName);
            _blob = container.GetBlockBlobReference(blobName);
            _settings = new JsonSerializerSettings
            {
                ContractResolver = new DictionaryAsArrayResolver(),
                Formatting = Formatting.Indented
            };
        }

        public Task SaveStatusAsync(CancellationToken ct)
        {
            var json = JsonConvert.SerializeObject(_statuses, _settings);
            return _policy.ExecuteAsync(cancel =>
                _blob.UploadTextAsync(json, Encoding.UTF8, null, null, null, cancel), ct, false);
        }

        public LogFilesStatus GetOrCreateStatusForPattern(string pattern)
        {
            if (_statuses == null)
            {
                throw new InvalidOperationException("Not yet initialised");
            }
            var status = _statuses.Find(s => s.Pattern.Equals(pattern));
            if (status == null)
            {
                status = new LogFilesStatus { Pattern = pattern };
                _statuses.Add(status);
            }
            return status;
        }

        public async Task InitialiseAsync(CancellationToken ct)
        {
            await _policy.ExecuteAsync(cancel => _blob.Container.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Container, null, null, cancel), ct, false).ConfigureAwait(false);
            if (await _policy.ExecuteAsync(cancel => _blob.ExistsAsync(null, null, cancel), ct, false).ConfigureAwait(false))
            {
                try
                {
                    var json = await _policy.ExecuteAsync(cancel =>
                        _blob.DownloadTextAsync(Encoding.UTF8, null, null, null, cancel), ct, false).ConfigureAwait(false);
                    _statuses = JsonConvert.DeserializeObject<List<LogFilesStatus>>(json, _settings);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to read status files: {Exception}", ex.Message);
                    _statuses = new List<LogFilesStatus>();
                }
            }
            else
            {
                _statuses = new List<LogFilesStatus>();
            }
        }
    }
}
