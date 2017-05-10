using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SceneSkope.Utilities.Text;
using SceneSkope.Utilities.TextFiles;
using Xunit;

namespace TextTests
{
    public class TestLogTailer
    {
        private readonly CancellationToken _ct = CancellationToken.None;
        [Fact]

        public async Task VerifyStaticTailingWorksOkAsync()
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new StreamWriter(ms, Encoding.UTF8, 1024, true))
                {
                    await writer.WriteLineAsync("Line 1").ConfigureAwait(false);
                    await writer.WriteLineAsync("Line 2").ConfigureAwait(false);
                }
                ms.Seek(0, SeekOrigin.Begin);
                using (var log = new LogStream(ms))
                {
                    var (line, lineNumber) = await log.TryReadNextLineAsync(_ct).ConfigureAwait(false);
                    Assert.Equal("Line 1", line);
                    (line, lineNumber) = await log.TryReadNextLineAsync(_ct).ConfigureAwait(false);
                    Assert.Equal("Line 2", line);
                    (line, lineNumber) = await log.TryReadNextLineAsync(_ct).ConfigureAwait(false);
                    Assert.Null(line);
                }
            }
        }

        [Fact]
        public async Task VerifyTailingAndReadingWorksOkAsync()
        {
            using (var ms = new MemoryStream())
            using (var writer = new StreamWriter(ms, Encoding.UTF8, 1024, true))
            {
                await writer.WriteLineAsync("Line 1").ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
                long position;
                ms.Seek(0, SeekOrigin.Begin);
                using (var log = new LogStream(ms))
                {
                    var (line, lineNumber) = await log.TryReadNextLineAsync(_ct).ConfigureAwait(false);
                    Assert.Equal("Line 1", line);
                    (line, lineNumber) = await log.TryReadNextLineAsync(_ct).ConfigureAwait(false);
                    Assert.Null(line);
                    position = log.Position;
                }

                await writer.WriteLineAsync("Line 2").ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);

                using (var log = new LogStream(ms, position))
                {
                    var (line, lineNumber) = await log.TryReadNextLineAsync(_ct).ConfigureAwait(false);
                    Assert.Equal("Line 2", line);
                    (line, lineNumber) = await log.TryReadNextLineAsync(_ct).ConfigureAwait(false);
                    Assert.Null(line);
                }
            }
        }

        [Fact]
        public async Task VerifyPartialWritingWorksOkAsync()
        {
            var buffer = new byte[1024];
            using (var outms = new MemoryStream(buffer, true))
            using (var writer = new StreamWriter(outms, Encoding.UTF8, 1024, true))
            {
                await writer.WriteAsync("Line 1").ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
                long position;
                using (var ms = new MemoryStream(buffer, 0, (int)outms.Position))
                using (var log = new LogStream(ms))
                {
                    var (line, lineNumber) = await log.TryReadNextLineAsync(_ct).ConfigureAwait(false);
                    Assert.Null(line);
                    position = log.Position;
                }

                await writer.WriteAsync("\rLine 2").ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);

                using (var ms = new MemoryStream(buffer, 0, (int)outms.Position))
                using (var log = new LogStream(ms, position))
                {
                    var (line, lineNumber) = await log.TryReadNextLineAsync(_ct).ConfigureAwait(false);
                    Assert.Equal("Line 1", line);
                    (line, lineNumber) = await log.TryReadNextLineAsync(_ct).ConfigureAwait(false);
                    Assert.Null(line);
                    (line, lineNumber) = await log.TryReadNextLineAsync(_ct, true).ConfigureAwait(false);
                    Assert.Equal("Line 2", line);
                }
            }
        }

        [Fact]
        public async Task VerifyWritingToAFileWorksOkAsync()
        {
            using (var folder = new TemporaryFolder())
            {
                var fullName = folder.CreateFileName(".log");
                var fileName = Path.GetFileName(fullName);
                using (var writer = File.CreateText(fullName))
                using (var log = new LogFile(folder.Directory, fileName))
                {
                    var (line, lineNumber) = await log.TryReadNextLineAsync(_ct).ConfigureAwait(false);
                    Assert.Null(line);
                    await writer.WriteLineAsync("Line 1").ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);

                    (line, lineNumber) = await log.TryReadNextLineAsync(_ct).ConfigureAwait(false);
                    Assert.Equal("Line 1", line);
                    (line, lineNumber) = await log.TryReadNextLineAsync(_ct).ConfigureAwait(false);
                    Assert.Null(line);

                    await writer.WriteLineAsync("Line 2").ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);

                    (line, lineNumber) = await log.TryReadNextLineAsync(_ct).ConfigureAwait(false);
                    Assert.Equal("Line 2", line);
                    (line, lineNumber) = await log.TryReadNextLineAsync(_ct).ConfigureAwait(false);
                    Assert.Null(line);
                }
            }
        }

        private LogStatusFile CreateStatusFile(FileInfo file) => new LogStatusFile(file);

        [Fact]
        public async Task VerifyHandlingRollOverLogWorksOkAsync()
        {
            var cancel = CancellationToken.None;
            using (var folder = new TemporaryFolder())
            using (var logDirectory = await LogDirectory.CreateAsync(folder.Directory, CreateStatusFile(folder.CreateFileInfo(".json")), _ct).ConfigureAwait(false))
            {
                var log = await logDirectory.GetLogFilesAsync("api*.txt", cancel).ConfigureAwait(false);
                var firstFile = folder.GetFileName("api-2016-12-10.txt");
                var secondFile = folder.GetFileName("api-2016-12-11.txt");

                var info = await log.TryReadNextLineAsync(cancel).ConfigureAwait(false);
                Assert.Null(info);

                using (var writer = File.CreateText(firstFile))
                {
                    await Task.Delay(100).ConfigureAwait(false);
                    info = await log.TryReadNextLineAsync(cancel).ConfigureAwait(false);
                    Assert.Null(info);

                    await writer.WriteLineAsync("Line 1").ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);

                    info = await log.TryReadNextLineAsync(cancel).ConfigureAwait(false);
                    Assert.Equal("Line 1", info.Line);
                    Assert.Equal(1, info.LineNumber);
                    info = await log.TryReadNextLineAsync(cancel).ConfigureAwait(false);
                    Assert.Null(info);

                    await writer.WriteLineAsync("Line 2").ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                }

                using (var writer = File.CreateText(secondFile))
                {
                    await writer.WriteLineAsync("Line 3").ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);

                    await writer.WriteLineAsync("Line 4").ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                }

                info = await log.TryReadNextLineAsync(cancel).ConfigureAwait(false);
                Assert.Equal("Line 2", info.Line);
                Assert.Equal(2, info.LineNumber);
                info = await log.TryReadNextLineAsync(cancel).ConfigureAwait(false);
                Assert.Equal("Line 3", info.Line);
                Assert.Equal(1, info.LineNumber);
                info = await log.TryReadNextLineAsync(cancel).ConfigureAwait(false);
                Assert.Equal("Line 4", info.Line);
                Assert.Equal(2, info.LineNumber);
                info = await log.TryReadNextLineAsync(cancel).ConfigureAwait(false);
                Assert.Null(info);
            }
        }

        [Fact]
        public async Task VerifyMultiFilesAndStoppingWorksOkAsync()
        {
            var cancel = CancellationToken.None;
            using (var folder = new TemporaryFolder())
            {
                var firstFile = folder.GetFileName("api-2016-12-10.txt");
                var secondFile = folder.GetFileName("api-2016-12-11.txt");
                var thirdFile = folder.GetFileName("api-2016-12-12.txt");

                var statusFile = CreateStatusFile(folder.CreateFileInfo(".json"));

                using (var writer = File.CreateText(firstFile))
                {
                    await writer.WriteLineAsync("Line 1").ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                    await writer.WriteLineAsync("Line 2").ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                }
                using (var writer = File.CreateText(secondFile))
                {
                    await writer.WriteLineAsync("Line 3").ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                    await writer.WriteLineAsync("Line 4").ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                }
                using (var writer = File.CreateText(thirdFile))
                {
                    await writer.WriteLineAsync("Line 5").ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                    await writer.WriteLineAsync("Line 6").ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                }

                using (var logDirectory = await LogDirectory.CreateAsync(folder.Directory, statusFile, _ct).ConfigureAwait(false))
                using (var log = await logDirectory.GetLogFilesAsync("api*.txt", cancel).ConfigureAwait(false))
                {
                    var info = await log.TryReadNextLineAsync(cancel).ConfigureAwait(false);
                    Assert.Equal("Line 1", info.Line);
                    Assert.Equal(1, info.LineNumber);
                    info = await log.TryReadNextLineAsync(cancel).ConfigureAwait(false);
                    Assert.Equal("Line 2", info.Line);
                    Assert.Equal(2, info.LineNumber);
                    info = await log.TryReadNextLineAsync(cancel).ConfigureAwait(false);
                    Assert.Equal("Line 3", info.Line);
                    await logDirectory.SaveStatusAsync(cancel).ConfigureAwait(false);
                    Assert.Equal(1, info.LineNumber);
                }

                using (var logDirectory = await LogDirectory.CreateAsync(folder.Directory, statusFile, _ct).ConfigureAwait(false))
                using (var log = await logDirectory.GetLogFilesAsync("api*.txt", cancel).ConfigureAwait(false))
                {
                    var info = await log.TryReadNextLineAsync(cancel).ConfigureAwait(false);
                    Assert.Equal("Line 4", info.Line);
                    Assert.Equal(2, info.LineNumber);
                    info = await log.TryReadNextLineAsync(cancel).ConfigureAwait(false);
                    Assert.Equal("Line 5", info.Line);
                    Assert.Equal(1, info.LineNumber);
                    info = await log.TryReadNextLineAsync(cancel).ConfigureAwait(false);
                    Assert.Equal("Line 6", info.Line);
                    Assert.Equal(2, info.LineNumber);
                    info = await log.TryReadNextLineAsync(cancel).ConfigureAwait(false);
                    Assert.Null(info);
                    await logDirectory.SaveStatusAsync(cancel).ConfigureAwait(false);
                }
            }
        }
    }
}
