using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.Text
{
    public class LogFile : ILogFile
    {
        private readonly FileStream _stream;
        private readonly LogStream _logStream;

        public string Name { get; }
        public long Position => _logStream.Position;
        public LogFile(DirectoryInfo baseDirectory, string name, long? position = null, int? lineNumber = null)
        {
            Name = name;
            var fullName = Path.Combine(baseDirectory.FullName, name);
            _stream = new FileStream(fullName, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
            _logStream = new LogStream(_stream, position, lineNumber);
        }

        public Task<(string line, int lineNumber)> TryReadNextLineAsync(CancellationToken ct) => _logStream.TryReadNextLineAsync(ct);

        public void Dispose()
        {
            _logStream.Dispose();
            _stream.Dispose();
        }
    }

}
