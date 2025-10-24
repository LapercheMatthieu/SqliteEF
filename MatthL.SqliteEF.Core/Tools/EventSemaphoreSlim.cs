using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Core.Tools
{
    public class EventSemaphoreSlim : SemaphoreSlim
    {
        public event Action<int> SemaphoreAcquired;  // Passe le nombre de slots restants
        public event Action<int> SemaphoreReleased;  // Passe le nombre de slots restants

        private readonly int _maxCount;

        public EventSemaphoreSlim(int initialCount, int maxCount) : base(initialCount, maxCount)
        {
            _maxCount = maxCount;
        }

        public new async Task WaitAsync()
        {
            await base.WaitAsync();
            SemaphoreAcquired?.Invoke(CurrentCount);
        }

        public new async Task<bool> WaitAsync(int millisecondsTimeout)
        {
            var result = await base.WaitAsync(millisecondsTimeout);
            if (result)
            {
                SemaphoreAcquired?.Invoke(CurrentCount);
            }
            return result;
        }

        public new async Task<bool> WaitAsync(TimeSpan timeout)
        {
            var result = await base.WaitAsync(timeout);
            if (result)
            {
                SemaphoreAcquired?.Invoke(CurrentCount);
            }
            return result;
        }

        public new int Release()
        {
            var result = base.Release();
            SemaphoreReleased?.Invoke(CurrentCount);
            return result;
        }

        public new int Release(int releaseCount)
        {
            var result = base.Release(releaseCount);
            SemaphoreReleased?.Invoke(CurrentCount);
            return result;
        }

        public bool IsAcquired => CurrentCount < _maxCount;
        public int ActiveCount => _maxCount - CurrentCount;
    }
}
