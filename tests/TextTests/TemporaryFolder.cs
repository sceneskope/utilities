using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TextTests
{
    public class TemporaryFolder : IDisposable
    {
        private readonly DirectoryInfo _directory;
        public DirectoryInfo Directory => _directory;

        private int _index;

        public TemporaryFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _directory = new DirectoryInfo(folder);
            _directory.Create();
        }

        public string GetFileName(string filename) => Path.Combine(_directory.FullName, filename);

        public string CreateFileName(string extension) => Path.Combine(_directory.FullName, $"{_index++}.{extension}");

        public FileInfo CreateFileInfo(string extension) => new FileInfo(CreateFileName(extension));

        public FileStream CreateFile(string extension) => new FileStream(CreateFileName(extension), FileMode.CreateNew);
        public TextWriter CreateWriter(string extension) => new StreamWriter(CreateFile(extension));


        public void Dispose()
        {
            _directory.Delete(true);
        }
    }
}
