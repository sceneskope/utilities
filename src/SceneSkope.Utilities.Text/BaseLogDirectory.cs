using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.Text
{
    public abstract class BaseLogDirectory
    {
        public static Regex CreatePatternRegex(string pattern)
            => new Regex(pattern.Replace(".", "\\.").Replace("*", ".*") + "$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    public abstract class BaseLogDirectory<TStatus> : BaseLogDirectory, ILogDirectory<TStatus>
        where TStatus : LogFilesStatus, new()
    {
        private readonly ILogStatus<TStatus> _status;

        protected BaseLogDirectory(ILogStatus<TStatus> status)
        {
            //_status = new LogStatusFile<TStatus>(statusFile, pattern => new TStatus { Pattern = pattern });
            _status = status;
        }

        public Task SaveStatusAsync(CancellationToken ct) => _status.SaveStatusAsync(ct);

        protected TStatus GetOrCreateStatusForPattern(string pattern) => _status.GetOrCreateStatusForPattern(pattern);

        protected Task InitialiseAsync(CancellationToken ct) => _status.InitialiseAsync(ct);

        public abstract Task<ILogFiles<TStatus>> GetLogFilesAsync(string pattern, CancellationToken ct);

        public abstract void Dispose();
    }
}
