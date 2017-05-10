using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.Text
{
    public class LogFiles : BaseLogFiles<LogFile>
    {
        private readonly List<string> _incomingFiles = new List<string>();
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private readonly DirectoryInfo _baseDirectory;
        internal LogFiles(DirectoryInfo baseDirectory, string pattern, LogFilesStatus status = null) : base(pattern, status)
        {
            _baseDirectory = baseDirectory;
        }

        internal void AddFile(FileInfo file)
        {
            var latest = Status.CurrentName;
            if ((latest == null) || (string.Compare(file.Name, latest, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                _lock.Wait();
                try
                {
                    _incomingFiles.Add(file.Name);
                }
                finally
                {
                    _lock.Release();
                }
            }
        }

        protected override async Task<IEnumerable<string>> FindNewFilesAsync(CancellationToken ct)
        {
            try
            {
                await _lock.WaitAsync(ct).ConfigureAwait(false);
                if (_incomingFiles.Count > 0)
                {
                    var result = _incomingFiles.ToList();
                    _incomingFiles.Clear();
                    return result;
                }
                else
                {
                    return null;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        protected override Task<LogFile> GetLogFileAsync(string name, CancellationToken ct, long? position, int? lineNumber)
            => Task.FromResult(new LogFile(_baseDirectory, name, position, lineNumber));
    }
}
