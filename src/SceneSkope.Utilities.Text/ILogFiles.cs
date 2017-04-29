using System;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.Text
{
    public interface ILogFiles<TStatus> : IDisposable
        where TStatus : LogFilesStatus
    {
        TStatus Status { get; }
        Task<UploadedLine> TryReadNextLineAsync(CancellationToken cancel);
    }
}