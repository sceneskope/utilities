using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using SceneSkope.Utilities.Text;
using Serilog;

namespace SceneSkope.Utilities.TextFiles
{
    public class LogStatusFile<T> : ILogStatus<T> where T : LogFilesStatus
    {
        private readonly JsonSerializerSettings _settings;

        private readonly AtomicTextFile _statusFile;
        private List<T> _statuses;
        private readonly Func<string, T> _creator;

        public LogStatusFile(FileInfo statusFile, Func<string, T> creator) : this(new AtomicTextFile(statusFile), creator)
        {
        }

        public LogStatusFile(AtomicTextFile statusFile, Func<string, T> creator)
        {
            _statusFile = statusFile;
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
            return _statusFile.SaveAsync(json, ct);
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

        public async Task InitialiseAsync()
        {
            if (_statusFile.Exists)
            {
                try
                {
                    var json = await _statusFile.LoadFileAsync().ConfigureAwait(false);
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
