using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.Text
{
    public interface ILogDirectory<TStatus> : IDisposable
        where TStatus : LogFilesStatus
    {
        Task<ILogFiles<TStatus>> GetLogFilesAsync(string pattern, CancellationToken ct);

        Task SaveStatusAsync(CancellationToken ct);
    }
}
