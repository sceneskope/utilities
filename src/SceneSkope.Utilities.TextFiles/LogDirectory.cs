using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SceneSkope.Utilities.Text;

namespace SceneSkope.Utilities.TextFiles
{
    public class LogDirectory : BaseLogDirectory
    {
        public static async Task<ILogDirectory> CreateAsync(DirectoryInfo directory, ILogStatus status, CancellationToken ct)
        {
            var logDirectory = new LogDirectory(directory, status);
            await logDirectory.InitialiseAsync(ct).ConfigureAwait(false);
            return logDirectory;
        }

        public DirectoryInfo Directory { get; }
        private readonly FileSystemWatcher _watcher;
        private readonly List<LogFiles> _logFiles = new List<LogFiles>();
        private readonly List<Regex> _patterns = new List<Regex>();
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);

        private LogDirectory(DirectoryInfo directory, ILogStatus status) : base(status)
        {
            Directory = directory;
            _watcher = new FileSystemWatcher(directory.FullName);
            _watcher.Created += HandleNewFileCreated;
            _watcher.InternalBufferSize = 65535;
            _watcher.EnableRaisingEvents = true;
        }

        public override async Task<ILogFiles> GetLogFilesAsync(string pattern, CancellationToken ct)
        {
            var status = GetOrCreateStatusForPattern(pattern);
            var logFiles = new LogFiles(Directory, pattern, status);
            var regex = CreatePatternRegex(pattern);
            foreach (var file in Directory.EnumerateFiles(pattern))
            {
                logFiles.AddFile(file);
            }
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _logFiles.Add(logFiles);
                _patterns.Add(regex);
            }
            finally
            {
                _lock.Release();
            }
            return logFiles;
        }

        private void HandleNewFileCreated(object sender, FileSystemEventArgs e)
        {
            var file = new FileInfo(e.FullPath);
            var name = file.Name;
            for (var i = 0; i < _patterns.Count; i++)
            {
                if (_patterns[i].IsMatch(name))
                {
                    var logFiles = _logFiles[i];
                    logFiles.AddFile(file);
                }
            }
        }

        public override void Dispose()
        {
            _watcher.Dispose();
            for (var i = 0; i < _logFiles.Count; i++)
            {
                _logFiles[i].Dispose();
            }
        }
    }
}
