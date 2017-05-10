using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using SceneSkope.Utilities.Text;

namespace SceneSkope.Utilities.BlobStorage
{
    internal sealed class LogBlob : ILogFile
    {
        public static async Task<LogBlob> InitialiseAsync(CloudBlockBlob blob, CancellationToken ct, long? position = null, int? lineNumber = null)
        {
            var stream = await blob.OpenReadAsync(null, null, null, ct).ConfigureAwait(false);
            return new LogBlob(blob, stream, position, lineNumber);
        }

        public string Name => _blob.Name;

        public long Position => _logStream.Position;

        private readonly CloudBlockBlob _blob;
        private readonly LogStream _logStream;
        private readonly Stream _stream;

        private LogBlob(CloudBlockBlob blob, Stream stream, long? position = null, int? lineNumber = null)
        {
            _blob = blob;
            _stream = stream;
            _logStream = new LogStream(stream, position, lineNumber);
        }

        public void Dispose()
        {
            _logStream.Dispose();
            _stream.Dispose();
        }

        public Task<(string line, int lineNumber)> TryReadNextLineAsync(CancellationToken ct) => _logStream.TryReadNextLineAsync(ct);
    }
}
