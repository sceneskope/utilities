using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.Text
{
    public abstract class BaseLogFiles<TLogFile> : ILogFiles
        where TLogFile : class, ILogFile
    {
        private TLogFile _logFile;
        private string _lastName;
        private readonly List<string> _files = new List<string>();

        public LogFilesStatus Status { get; }

        public string Pattern { get; }

        protected BaseLogFiles(string pattern, LogFilesStatus status)
        {
            Status = status ?? new LogFilesStatus { Pattern = pattern };
            Pattern = pattern;
        }

        public void Dispose()
        {
            _logFile?.Dispose();
        }

        protected abstract Task<TLogFile> GetLogFileAsync(string name, CancellationToken ct, long? position = null, int? lineNumber = null);
        protected abstract Task<IEnumerable<string>> FindNewFilesAsync(CancellationToken ct);

        private async Task<string> TryGetNextFileAsync(CancellationToken cancel)
        {
            while (!cancel.IsCancellationRequested)
            {
                if (((_logFile == null) && (_files.Count > 0)) || ((_logFile != null) && (_files.Count > 1)))
                {
                    if (_logFile != null)
                    {
                        _logFile.Dispose();
                        _files.RemoveAt(0);
                    }

                    var nextFile = _files[0];
                    _lastName = nextFile;
                    return nextFile;
                }
                else
                {
                    var newFiles = await FindNewFilesAsync(cancel).ConfigureAwait(false);
                    if (newFiles == null)
                    {
                        return null;
                    }
                    else
                    {
                        if (_lastName != null)
                        {
                            newFiles = newFiles.Where(s => string.Compare(_lastName, s, StringComparison.OrdinalIgnoreCase) < 0);
                        }
                        _files.AddRange(newFiles);
                        _files.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));
                    }
                }
            }
            return null;
        }

        private async Task<bool> TryGetNextLogFileAsync(CancellationToken cancel)
        {
            var nextFile = await TryGetNextFileAsync(cancel).ConfigureAwait(false);
            if (nextFile != null)
            {
                var (position, lineNumber) = ((Status.CurrentName != null) && Status.CurrentName.Equals(nextFile)) ?
                    (Status.Position, Status.LineNumber)
                    : (null, 0);
                _logFile = await GetLogFileAsync(nextFile, cancel, position, lineNumber).ConfigureAwait(false);
                Status.CurrentName = nextFile;
                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task<UploadedLine> TryReadNextLineAsync(CancellationToken cancel)
        {
            if (_logFile == null)
            {
                await TryGetNextLogFileAsync(cancel).ConfigureAwait(false);
            }
            if (_logFile == null)
            {
                return null;
            }
            while (true)
            {
                var (nextLine, lineNumber) = await _logFile.TryReadNextLineAsync(cancel).ConfigureAwait(false);
                if (nextLine != null)
                {
                    Status.Position = _logFile.Position;
                    Status.LineNumber = lineNumber;
                    return new UploadedLine { FileName = _logFile.Name, Line = nextLine, LineNumber = lineNumber, Position = _logFile.Position };
                }
                if (!await TryGetNextLogFileAsync(cancel).ConfigureAwait(false))
                {
                    return null;
                }
            }
        }
    }
}
