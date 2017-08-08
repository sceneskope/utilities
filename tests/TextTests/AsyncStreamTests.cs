using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SceneSkope.Utilities.IO;
using Xunit;

namespace TextTests
{
    public class AsyncStreamTests
    {
        [Fact]
        public void CheckZeroReadReportsAsClosed()
        {
            var bytes = CreateBytes(0);

            using (var inputStream = new MemoryStream(bytes))
            using (var bufferStream = new BufferingAsyncInputStream(inputStream, 8))
            {
                Assert.Throws<IOException>(() => bufferStream.ReadByte());
            }
        }

        [Fact]
        public async Task ReadingFromAnEmtpyStreamWorksOk()
        {
            var bytes = CreateBytes(0);

            using (var inputStream = new MemoryStream(bytes))
            using (var bufferStream = new BufferingAsyncInputStream(inputStream, 8))
            {
                if (bufferStream.NeedsFilling)
                {
                    await bufferStream.FillAsync(default).ConfigureAwait(false);
                }
                Assert.Equal(-1, bufferStream.ReadByte());
            }
        }

        [Fact]
        public async Task FillingThenReadingAllThrowsOnNextRead()
        {
            const int count = 10;
            var bytes = CreateBytes(count * 2);

            using (var inputStream = new MemoryStream(bytes))
            using (var bufferStream = new BufferingAsyncInputStream(inputStream, count))
            {
                await bufferStream.FillAsync(default).ConfigureAwait(false);
                for (var i = 0; i < bytes.Length - 1; i++)
                {
                    Assert.Equal((byte)i, bufferStream.ReadByte());
                }
                Assert.Equal((byte)(bytes.Length - 1), bufferStream.ReadByte());
                Assert.Throws<IOException>(() => bufferStream.ReadByte());
            }
        }

        [Fact]
        public async Task FillingThenReadingAllThenFillingReadsEmptyOnNextRead()
        {
            const int count = 10;
            var bytes = CreateBytes(count * 2);

            using (var inputStream = new MemoryStream(bytes))
            using (var bufferStream = new BufferingAsyncInputStream(inputStream, count))
            {
                await bufferStream.FillAsync(default).ConfigureAwait(false);
                for (var i = 0; i < bytes.Length - 1; i++)
                {
                    Assert.Equal((byte)i, bufferStream.ReadByte());
                }
                Assert.Equal((byte)(bytes.Length - 1), bufferStream.ReadByte());
                await bufferStream.FillAsync(default).ConfigureAwait(false);
                Assert.Equal(-1, bufferStream.ReadByte());
            }
        }

#pragma warning disable RCS1109 // Use 'Cast' method instead of 'Select' method.
        private static byte[] CreateBytes(int count = 100) => Enumerable.Range(0, count).Select(i => (byte)i).ToArray();
#pragma warning restore RCS1109 // Use 'Cast' method instead of 'Select' method.

        [Fact]
        public async Task CheckReadingWorksOkAsync()
        {
            const int count = 100;
            const int bufferSize = 8;
            var bytes = CreateBytes(count);
            using (var inputStream = new MemoryStream(bytes))
            using (var bufferStream = new BufferingAsyncInputStream(inputStream, bufferSize))
            {
                for (var i = 0; i < count; i++)
                {
                    if (bufferStream.NeedsFilling)
                    {
                        await bufferStream.FillAsync(default).ConfigureAwait(false);
                    }
                    var value = bufferStream.ReadByte();
                    Assert.Equal(i, value);
                }
                Assert.Equal(-1, bufferStream.ReadByte());
            }
        }

        [Fact]
        public async Task CheckWritingWorksOkAsync()
        {
            const int count = 100;
            const int bufferSize = 8;
            var bytes = CreateBytes(count);
            using (var outputStream = new MemoryStream())
            {
                using (var inputStream = new MemoryStream(bytes))
                using (var outputBufferStream = new BufferingAsyncOutputStream(outputStream, bufferSize))
                using (var inputBufferStream = new BufferingAsyncInputStream(inputStream, bufferSize))
                {
                    for (var i = 0; i < count; i++)
                    {
                        if (inputBufferStream.NeedsFilling)
                        {
                            await inputBufferStream.FillAsync(default).ConfigureAwait(false);
                        }
                        var value = inputBufferStream.ReadByte();
                        Assert.Equal(i, value);
                        outputBufferStream.WriteByte((byte)value);
                        if (outputBufferStream.NeedsEmptying)
                        {
                            await outputBufferStream.EmptyAsync(default).ConfigureAwait(false);
                        }
                    }
                    Assert.Equal(-1, inputBufferStream.ReadByte());
                    await outputBufferStream.CloseAsync(default).ConfigureAwait(false);
                }
                var outputBytes = outputStream.GetBuffer();
                for (var i = 0; i < count; i++)
                {
                    Assert.Equal((byte)i, outputBytes[i]);
                }
            }
        }

        [Fact]
        public void NotEmptyingThrows()
        {
            var buffer = new byte[32];

            using (var outputStream = new MemoryStream())
            using (var outputBufferStream = new BufferingAsyncOutputStream(outputStream, buffer.Length))
            {
                outputBufferStream.Write(buffer, 0, buffer.Length);
                outputBufferStream.Write(buffer, 0, buffer.Length);
                Assert.Throws<IOException>(() => outputBufferStream.Write(buffer, 0, buffer.Length));
            }
        }

        [Fact]
        public async Task EmptyingJustInTimeWorksOk()
        {
            var buffer = new byte[32];

            using (var outputStream = new MemoryStream())
            {
                using (var outputBufferStream = new BufferingAsyncOutputStream(outputStream, buffer.Length, leaveOpen: true))
                {
                    outputBufferStream.Write(buffer, 0, buffer.Length);
                    outputBufferStream.Write(buffer, 0, buffer.Length);
                    await outputBufferStream.EmptyAsync(default).ConfigureAwait(false);
                    outputBufferStream.Write(buffer, 0, buffer.Length);
                    await outputBufferStream.EmptyAsync(default).ConfigureAwait(false);
                }
                Assert.Equal(buffer.Length * 3, outputStream.Length);
            }
        }

        [Fact]
        public async Task EmptyingJustInTimeButNotFlushingThrowsOnDispose()
        {
            var buffer = new byte[32];

            using (var outputStream = new MemoryStream())
            {
                await Assert.ThrowsAsync<IOException>(async () =>
                {
                    using (var outputBufferStream = new BufferingAsyncOutputStream(outputStream, buffer.Length, leaveOpen: true))
                    {
                        outputBufferStream.Write(buffer, 0, buffer.Length);
                        outputBufferStream.Write(buffer, 0, buffer.Length);
                        await outputBufferStream.EmptyAsync(default).ConfigureAwait(false);
                        outputBufferStream.Write(buffer, 0, buffer.Length);
                    }
                }).ConfigureAwait(false);
            }
        }

        [Fact]
        public void CheckDisposingBeforeClosingWithDataThrows()
        {
            Assert.Throws<IOException>(() =>
            {
                using (var outputStream = new MemoryStream())
                using (var outputBufferStream = new BufferingAsyncOutputStream(outputStream))
                {
                    outputBufferStream.WriteByte(0);
                }
            });
        }

        [Theory]
        [InlineData(17, 16, 2)]
        [InlineData(33, 16, 1)]
        public void CheckWritingWithLargeBufferThrows(int count, int bufferSize, int writeCount)
        {
            var bytes = CreateBytes(count);
            using (var outputStream = new MemoryStream())
            using (var outputBufferStream = new BufferingAsyncOutputStream(outputStream, bufferSize))
            {
                while (writeCount > 1)
                {
                    outputBufferStream.Write(bytes, 0, count);
                    writeCount--;
                }
                Assert.Throws<IOException>(() => outputBufferStream.Write(bytes, 0, count));
            }
        }
    }
}
