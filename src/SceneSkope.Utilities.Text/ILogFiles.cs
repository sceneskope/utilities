using System;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.Text
{
    public interface ILogFiles : IDisposable
    {
        LogFilesStatus Status { get; }
        Task<UploadedLine> TryReadNextLineAsync(CancellationToken cancel);
    }
}