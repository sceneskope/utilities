﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.Text
{
    public class LogDirectory<TStatus> : BaseLogDirectory<TStatus>
        where TStatus : LogFilesStatus, new()
    {
        public static async Task<ILogDirectory<TStatus>> CreateAsync(DirectoryInfo directory, ILogStatus<TStatus> status)
        {
            var logDirectory = new LogDirectory<TStatus>(directory, status);
            await logDirectory.InitialiseAsync();
            return logDirectory;
        }
        public DirectoryInfo Directory { get; }
        private readonly FileSystemWatcher _watcher;
        private List<LogFiles<TStatus>> _logFiles = new List<LogFiles<TStatus>>();
        private List<Regex> _patterns = new List<Regex>();
        private SemaphoreSlim _lock = new SemaphoreSlim(1);

        private LogDirectory(DirectoryInfo directory, ILogStatus<TStatus> status) : base(status)
        {
            Directory = directory;
            _watcher = new FileSystemWatcher(directory.FullName);
            _watcher.Created += HandleNewFileCreated;
            _watcher.InternalBufferSize = 65535;
            _watcher.EnableRaisingEvents = true;
        }

        public override async Task<ILogFiles<TStatus>> GetLogFilesAsync(string pattern, CancellationToken ct)
        {
            var status = GetOrCreateStatusForPattern(pattern);
            var logFiles = new LogFiles<TStatus>(Directory, pattern, status);
            var regex = CreatePatternRegex(pattern);
            foreach (var file in Directory.EnumerateFiles(pattern))
            {
                logFiles.AddFile(file);
            }
            await _lock.WaitAsync(ct);
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
