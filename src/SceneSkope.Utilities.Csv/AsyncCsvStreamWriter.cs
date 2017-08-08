using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using SceneSkope.Utilities.IO;

namespace SceneSkope.Utilities.Csv
{
    public class AsyncCsvStreamWriter : IDisposable
    {
        private readonly BufferingAsyncOutputStream _bufferingOutputStream;
        private readonly StreamWriter _streamWriter;
        private readonly CsvWriter _csvWriter;
        private bool _firstRow = false;
        private bool _closed = false;

        public AsyncCsvStreamWriter(Stream stream, CsvConfiguration configuration = null, bool leaveOpen = false, int bufferSize = 8192)
        {
            _bufferingOutputStream = new BufferingAsyncOutputStream(stream, bufferSize, leaveOpen);
            _streamWriter = new StreamWriter(_bufferingOutputStream, Encoding.UTF8, bufferSize, true);
            _csvWriter = new CsvWriter(_streamWriter, configuration ?? new CsvConfiguration());
        }

        public async Task WriteRecordAsync<T>(T record, CancellationToken ct)
        {
            if (_firstRow)
            {
                lock (AsyncCsvStreamReader.s_lock)
                {
                    _csvWriter.WriteHeader<T>();
                    _csvWriter.NextRecord();
                    _csvWriter.WriteRecord(record);
                    _csvWriter.NextRecord();
                    _firstRow = false;
                }
            }
            else
            {
                _csvWriter.WriteRecord(record);
                _csvWriter.NextRecord();
            }
            if (_bufferingOutputStream.NeedsEmptying)
            {
                await _bufferingOutputStream.EmptyAsync(ct).ConfigureAwait(false);
            }
        }

        public async Task CloseAsync(CancellationToken ct)
        {
            _csvWriter.Dispose();
            _streamWriter.Dispose();
            await _bufferingOutputStream.CloseAsync(ct).ConfigureAwait(false);
            _bufferingOutputStream.Dispose();
            _closed = true;
        }

        public void Dispose()
        {
            if (!_closed)
            {
                throw new InvalidOperationException("Writer should be closed before disposing");
            }
        }
    }
}
