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
    public class AsyncCsvStreamReader : IDisposable
    {
        internal static readonly object s_lock = new object();
        private readonly BufferingAsyncInputStream _bufferingInputStream;
        private readonly StreamReader _streamReader;
        private readonly CsvReader _csvReader;
        private bool _firstRow = true;

        public ReadingContext CsvContext => _csvReader.Context as ReadingContext;

        public AsyncCsvStreamReader(Stream stream, Configuration configuration = null, bool leaveOpen = false, int bufferSize = 8192)
        {
            _bufferingInputStream = new BufferingAsyncInputStream(stream, bufferSize, leaveOpen);
            _streamReader = new StreamReader(_bufferingInputStream);
            var parser = new CsvParser(_streamReader, configuration ?? new Configuration(), true);
            _csvReader = new CsvReader(parser);
        }

        public async Task<T> ReadRecordAsync<T>(CancellationToken ct)
            where T : class
        {
            if (_bufferingInputStream.NeedsFilling)
            {
                await _bufferingInputStream.FillAsync(ct).ConfigureAwait(false);
            }
            if (_firstRow)
            {
                lock (s_lock)
                {
                    if (_csvReader.Read() && _csvReader.ReadHeader() && _csvReader.Read())
                    {
                        var record = _csvReader.GetRecord<T>();
                        _firstRow = false;
                        return record;
                    }
                }
            }
            else
            {
                if (_csvReader.Read())
                {
                    var record = _csvReader.GetRecord<T>();
                    return record;
                }
            }
            return null;
        }

        public void Dispose()
        {
            _csvReader.Dispose();
            _streamReader.Dispose();
            _bufferingInputStream.Dispose();
        }
    }
}
