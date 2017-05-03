using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities
{
    public class Throttler
    {
        private readonly SemaphoreSlim _throttler;

        private class Lock : IDisposable
        {
            private readonly SemaphoreSlim _throttler;
            public void Dispose()
            {
                _throttler.Release();
            }

            internal Lock(SemaphoreSlim throttler)
            {
                _throttler = throttler;
            }
        }

        public Throttler(int count = 100)
        {
            _throttler = new SemaphoreSlim(count);
        }

        public async Task<IDisposable> ThrottleAsync(CancellationToken cancel)
        {
            await _throttler.WaitAsync(cancel).ConfigureAwait(false);
            return new Lock(_throttler);
        }
    }
}
