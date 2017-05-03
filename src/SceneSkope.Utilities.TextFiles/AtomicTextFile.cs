using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.TextFiles
{
    public class AtomicTextFile
    {
        public FileInfo File { get; }
        private readonly FileInfo _tempFile;

        public AtomicTextFile(FileInfo file)
        {
            File = file;
            _tempFile = new FileInfo(Path.Combine(file.Directory.FullName, $".{file.Name}"));
            Recover();
        }

        private void Recover()
        {
            _tempFile.Refresh();
            if (_tempFile.Exists)
            {
                File.Refresh();
                if (File.Exists)
                {
                    File.Delete();
                }
                System.IO.File.Move(_tempFile.FullName, File.FullName);
                _tempFile.Refresh();
                File.Refresh();
            }
        }

        private void Backup()
        {
            _tempFile.Refresh();
            if (_tempFile.Exists)
            {
                _tempFile.Delete();
            }
            File.Refresh();
            if (File.Exists)
            {
                System.IO.File.Move(File.FullName, _tempFile.FullName);
            }
            File.Refresh();
            _tempFile.Refresh();
        }

        private void Cleanup()
        {
            File.Refresh();
            if (File.Exists)
            {
                _tempFile.Refresh();
                if (_tempFile.Exists)
                {
                    _tempFile.Delete();
                    _tempFile.Refresh();
                }
            }
        }

        public bool Exists => File.Exists;
        public async Task SaveAsync(string content, CancellationToken ct)
        {
            Backup();
            await SaveFileAsync(File, content, ct).ConfigureAwait(false);
            Cleanup();
        }

        public Task<string> LoadFileAsync() => LoadFileAsync(File);

        private async Task<string> LoadFileAsync(FileInfo file)
        {
            using (var reader = file.OpenText())
            {
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
        }

        private async Task SaveFileAsync(FileInfo file, string text, CancellationToken ct)
        {
            using (var writer = file.CreateText())
            {
                await writer.WriteAsync(text).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
            }
        }
    }
}
