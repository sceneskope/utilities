using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.Text
{
    public abstract class BaseLogDirectory<TStatus> : ILogDirectory<TStatus>
        where TStatus : LogFilesStatus, new()
    {
        private readonly ILogStatus<TStatus> _status;

        protected BaseLogDirectory(ILogStatus<TStatus> status)
        {
            //_status = new LogStatusFile<TStatus>(statusFile, pattern => new TStatus { Pattern = pattern });
            _status = status;

        }

        public Task SaveStatusAsync(CancellationToken ct) => _status.SaveStatusAsync(ct);

        public static Regex CreatePatternRegex(string pattern) => new Regex(pattern.Replace(".", "\\.").Replace("*", ".*") + "$", RegexOptions.Compiled | RegexOptions.IgnoreCase);


        protected TStatus GetOrCreateStatusForPattern(string pattern) => _status.GetOrCreateStatusForPattern(pattern);

        protected Task InitialiseAsync() => _status.InitialiseAsync();

        public abstract Task<ILogFiles<TStatus>> GetLogFilesAsync(string pattern, CancellationToken ct);

        public abstract void Dispose();
    }
}
