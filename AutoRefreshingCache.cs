using System;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.Unity.Utility;

namespace Hasin.Taaghche.Utilities
{
    public class AutoRefreshingCache<T> where T : RefreshableItem
    {
        private readonly MemoryCache _cache;
        private readonly int _expireAfterSeconds;
        private readonly int _refreshAfterSeconds;

        public AutoRefreshingCache(int expireAfterSeconds, int refreshAfterSeconds)
        {
            _cache = MemoryCache.Default;
            _expireAfterSeconds = expireAfterSeconds;
            _refreshAfterSeconds = refreshAfterSeconds;
        }

        public T Get(string key, Func<T> calc)
        {
            DateTime? calculateTime = null;
            var result = Get(key, ref calculateTime);
            if (result == null)
            {
                Refresh(key, null, calc);
                return Get(key, ref calculateTime);
            }

            if (calculateTime < DateTime.UtcNow.AddSeconds(-_refreshAfterSeconds))
            {
                if (Interlocked.Increment(ref result.RefreshWorkers) == 1)
                {
                    Task.Run(() =>
                    {
						Refresh(key, result, calc);
                    });
                }
            }
            return result;
        }

        private T Get(string key, ref DateTime? calculateTime)
        {
            var o = _cache.Get(key) as Pair<T, DateTime>;
            if (o == null)
                return null;
            calculateTime = o.Second;
            return o.First;
        }
        
        private void Refresh(string key, RefreshableItem dataRefreshLock, Func<T> calc)
        {
            var data = calc();
            var absoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(_expireAfterSeconds);
            lock (_cache)
            {
                _cache.Set(key, new Pair<T, DateTime>(data, DateTime.UtcNow), absoluteExpiration);
            }
            if (dataRefreshLock != null)
            {
                Interlocked.Decrement(ref dataRefreshLock.RefreshWorkers);
            }
        }

    }

    public class RefreshableItem
    {
        public Int32 RefreshWorkers = 0;
    }
}
