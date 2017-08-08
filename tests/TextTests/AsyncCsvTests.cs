using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SceneSkope.Utilities.Csv;
using Xunit;

namespace TextTests
{
    public class AsyncCsvTests
    {
        private class Record
        {
            public string A { get; set; }
            public int B { get; set; }
        }

        [Fact]
        public async Task WriteEmptyAsync()
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new AsyncCsvStreamWriter(ms, leaveOpen: true))
                {
                    await writer.CloseAsync(default).ConfigureAwait(false);
                }
                // Should only include the BOM EF BB BF
                Assert.Equal(3, ms.Length);
            }
        }

        [Fact]
        public async Task ReadEmptyAsync()
        {
            using (var ms = new MemoryStream())
            using (var reader = new AsyncCsvStreamReader(ms))
            {
                var record = await reader.ReadRecordAsync<Record>(default).ConfigureAwait(false);
                Assert.Null(record);
            }
        }
    }
}
