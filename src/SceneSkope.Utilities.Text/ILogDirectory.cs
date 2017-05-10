using System;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.Text
{
    public interface ILogDirectory : IDisposable
    {
        Task<ILogFiles> GetLogFilesAsync(string pattern, CancellationToken ct);

        Task SaveStatusAsync(CancellationToken ct);
    }
}
