using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SceneSkope.Utilities.Text;
using Serilog;

namespace SceneSkope.Utilities.TextFiles
{
    public class LogStatusFile : ILogStatus
    {
        private readonly JsonSerializerSettings _settings;

        private readonly AtomicTextFile _statusFile;
        private List<LogFilesStatus> _statuses;

        public LogStatusFile(FileInfo statusFile) : this(new AtomicTextFile(statusFile))
        {
        }

        public LogStatusFile(AtomicTextFile statusFile)
        {
            _statusFile = statusFile;
            _settings = new JsonSerializerSettings
            {
                ContractResolver = new DictionaryAsArrayResolver(),
                Formatting = Formatting.Indented
            };
        }

        public Task SaveStatusAsync(CancellationToken ct)
        {
            var json = JsonConvert.SerializeObject(_statuses, _settings);
            return _statusFile.SaveAsync(json, ct);
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
            if (_statusFile.Exists)
            {
                try
                {
                    var json = await _statusFile.LoadFileAsync().ConfigureAwait(false);
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
