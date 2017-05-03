using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities
{
    public static class ParallelProcessor
    {
        public static async Task<TResult> ParallelAsync<TResult, TData, TState>(this IEnumerable<TData> data,
            Func<TState> initialiser,
            Func<TData, CancellationToken, Task<TState>> processor,
            Action<TState, TState> combiner,
            Func<TState, TResult> finaliser,
            CancellationToken cancel)
        {
            var state = initialiser();
            var locker = new SemaphoreSlim(1);
            var tasks = data.Select(d => ProcessAsync(d, processor, state, combiner, locker, cancel));
            await Task.WhenAll(tasks).ConfigureAwait(false);
            var result = finaliser(state);
            return result;
        }

        private static async Task ProcessAsync<TData, TState>(TData data,
            Func<TData, CancellationToken, Task<TState>> processor,
            TState state,
            Action<TState, TState> combiner,
            SemaphoreSlim locker,
            CancellationToken cancel)
        {
            var result = await processor(data, cancel).ConfigureAwait(false);
            await locker.WaitAsync(cancel).ConfigureAwait(false);
            try
            {
                combiner(state, result);
            }
            finally
            {
                locker.Release();
            }
        }
    }
}
