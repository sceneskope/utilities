using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.Text
{
    public interface ILogFile : IDisposable
    {
        long Position { get; }
        string Name { get; }

        Task<(string line, int lineNumber)> TryReadNextLineAsync(CancellationToken ct);
    }
}
