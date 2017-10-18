using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using SceneSkope.Utilities.Text;
using Serilog;

namespace SceneSkope.Utilities.BlobStorage
{
    internal sealed class LogBlobs : BaseLogFiles<LogBlob>
    {
        private readonly CloudBlobContainer _container;
        private string _highestName;
        private readonly Regex _patternRegex;
        private bool _firstTime = true;

        internal LogBlobs(CloudBlobContainer container, string pattern, LogFilesStatus status = null) : base(pattern, status)
        {
            _container = container;
            _patternRegex = BaseLogDirectory.CreatePatternRegex(pattern);
            Log.Information("Using status {Status}", status);
        }

        protected override async Task<IEnumerable<string>> FindNewFilesAsync(CancellationToken ct)
        {
            _highestName = _highestName ?? Status?.CurrentName ?? "";

            string newHighest = null;
            BlobContinuationToken continuation = null;
            List<string> blobs = null;
            do
            {
                var results = await _container.ListBlobsSegmentedAsync("", true, BlobListingDetails.Metadata, null, continuation, null, null, ct).ConfigureAwait(false);
                foreach (var listing in results.Results)
                {
                    if (listing is CloudBlob blob)
                    {
                        if (_patternRegex.IsMatch(blob.Name)
                            && (string.Compare(blob.Name, _highestName, StringComparison.OrdinalIgnoreCase) > (_firstTime ? -1 : 0)))
                        {
                            blobs = blobs ?? new List<string>();
                            blobs.Add(blob.Name);
                            newHighest = (newHighest == null)
                                ? blob.Name
                                : string.Compare(blob.Name, newHighest, StringComparison.OrdinalIgnoreCase) > 0
                                    ? blob.Name
                                    : newHighest;
                        }
                    }
                }
                continuation = results.ContinuationToken;

            } while (!ct.IsCancellationRequested && (continuation != null));
            if (newHighest != null)
            {
                _firstTime = false;
                _highestName = newHighest;
            }
            return blobs;
        }

        protected override async Task<LogBlob> GetLogFileAsync(string name, CancellationToken ct, long? position = default, int? lineNumber = default)
        {
            var blob = _container.GetBlockBlobReference(name);
            var logBlob = await LogBlob.InitialiseAsync(blob, ct, position, lineNumber).ConfigureAwait(false);
            return logBlob;
        }
    }
}
