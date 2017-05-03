using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.TextFiles
{
    public abstract class BaseFilter<TKey>
    {
        public DirectoryInfo OutputDirectory { get; }
        private readonly Throttler _throttler;

        protected BaseFilter(DirectoryInfo outputDirectory, int throttleTo = 100)
        {
            OutputDirectory = outputDirectory;
            if (!outputDirectory.Exists)
            {
                outputDirectory.Create();
            }
            _throttler = new Throttler(throttleTo);
        }

        protected abstract string CreateFileName(TKey key);
        protected abstract string TryProcessLine(string line, ref TKey key, out DateTimeOffset timestamp);

        public async Task ProcessAsync(IList<FileInfo> files, CancellationToken cancel)
        {
            var results = await ProcessFilesAsync(files, cancel).ConfigureAwait(false);
            var writers = results.Select(kvp => WriteFileAsync(CreateFileName(kvp.Key), kvp.Value, cancel));
            await Task.WhenAll(writers).ConfigureAwait(false);
        }

        private async Task WriteFileAsync(string name, IList<Tuple<DateTimeOffset, string>> lines, CancellationToken cancel)
        {
            try
            {
                var file = new FileInfo(Path.Combine(OutputDirectory.FullName, $"{name}.json"));
                var sorted = lines.OrderBy(t => t.Item1).Select(t => t.Item2);
                using (var throttle = await _throttler.ThrottleAsync(cancel).ConfigureAwait(false))
                {
                    await file.WriteLinesAsArrayAsync(sorted, cancel).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process to {name}: {ex.Message}");
            }
        }

        private Task<Dictionary<TKey, List<Tuple<DateTimeOffset, string>>>> ProcessFilesAsync(IList<FileInfo> files, CancellationToken cancel) =>
            files.ParallelAsync(
                () => new Dictionary<TKey, List<Tuple<DateTimeOffset, string>>>(),
                SplitFileAsync,
                CombineResults,
                s => s,
                cancel);

        private static void CombineResults(Dictionary<TKey, List<Tuple<DateTimeOffset, string>>> totals, Dictionary<TKey, List<Tuple<DateTimeOffset, string>>> individual)
        {
            foreach (var kvp in individual)
            {
                if (!totals.TryGetValue(kvp.Key, out var list))
                {
                    list = new List<Tuple<DateTimeOffset, string>>();
                    totals.Add(kvp.Key, list);
                }
                list.AddRange(kvp.Value);
            }
        }

        private async Task<Dictionary<TKey, List<Tuple<DateTimeOffset, string>>>> SplitFileAsync(FileInfo file, CancellationToken cancel)
        {
            var results = new Dictionary<TKey, List<Tuple<DateTimeOffset, string>>>();
            using (var reader = file.OpenText())
            {
                string line;
                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    cancel.ThrowIfCancellationRequested();
                    var key = default(TKey);
                    var outputLine = TryProcessLine(line, ref key, out var timestamp);
                    if (outputLine != null)
                    {
                        if (!results.TryGetValue(key, out var list))
                        {
                            list = new List<Tuple<DateTimeOffset, string>>();
                            results[key] = list;
                        }
                        list.Add(Tuple.Create(timestamp, outputLine));
                    }
                }
            }
            return results;
        }
    }
}
