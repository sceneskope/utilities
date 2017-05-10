using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.Text
{
    public interface ILogStatus
    {
        Task SaveStatusAsync(CancellationToken ct);

        LogFilesStatus GetOrCreateStatusForPattern(string pattern);

        Task InitialiseAsync(CancellationToken ct);
    }
}
