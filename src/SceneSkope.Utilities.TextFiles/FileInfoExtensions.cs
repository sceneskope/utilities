using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.TextFiles
{
    public static class FileInfoExtensions
    {
        public static async Task<string> ReadAllTextAsync(this FileInfo file)
        {
            using (var reader = file.OpenText())
            {
                return await reader.ReadToEndAsync();
            }
        }

        public static async Task WriteLinesAsArrayAsync(this FileInfo file, IEnumerable<string> lines, CancellationToken cancel)
        {
            using (var writer = file.CreateText())
            {
                await writer.WriteAsync('[');
                var first = true;
                foreach (var line in lines)
                {
                    cancel.ThrowIfCancellationRequested();
                    if (!first)
                    {
                        await writer.WriteLineAsync(',');
                    }
                    else
                    {
                        await writer.WriteLineAsync();
                        first = false;
                    }
                    await writer.WriteAsync(line);
                }
                await writer.WriteLineAsync(']');
                await writer.FlushAsync();
            }

        }
    }
}
