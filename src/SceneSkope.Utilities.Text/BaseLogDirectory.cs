using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.Text
{
    public abstract class BaseLogDirectory : ILogDirectory
    {
        public static Regex CreatePatternRegex(string pattern)
            => new Regex(pattern.Replace(".", "\\.").Replace("*", ".*") + "$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly ILogStatus _status;

        protected BaseLogDirectory(ILogStatus status)
        {
            _status = status;
        }

        public Task SaveStatusAsync(CancellationToken ct) => _status.SaveStatusAsync(ct);

        protected LogFilesStatus GetOrCreateStatusForPattern(string pattern) => _status.GetOrCreateStatusForPattern(pattern);

        protected Task InitialiseAsync(CancellationToken ct) => _status.InitialiseAsync(ct);

        public abstract Task<ILogFiles> GetLogFilesAsync(string pattern, CancellationToken ct);

        public abstract void Dispose();
    }
}
