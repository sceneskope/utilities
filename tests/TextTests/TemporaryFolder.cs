using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TextTests
{
    public class TemporaryFolder : IDisposable
    {
        public DirectoryInfo Directory { get; }

        private int _index;

        public TemporaryFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory = new DirectoryInfo(folder);
            Directory.Create();
        }

        public string GetFileName(string filename) => Path.Combine(Directory.FullName, filename);

        public string CreateFileName(string extension) => Path.Combine(Directory.FullName, $"{_index++}.{extension}");

        public FileInfo CreateFileInfo(string extension) => new FileInfo(CreateFileName(extension));

        public FileStream CreateFile(string extension) => new FileStream(CreateFileName(extension), FileMode.CreateNew);
        public TextWriter CreateWriter(string extension) => new StreamWriter(CreateFile(extension));

        public void Dispose()
        {
            Directory.Delete(true);
        }
    }
}
