using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.Text
{
    public interface ILogStatus<T> where T : LogFilesStatus
    {
        Task SaveStatusAsync(CancellationToken ct);

        T GetOrCreateStatusForPattern(string pattern);

        Task InitialiseAsync();
    }
}
