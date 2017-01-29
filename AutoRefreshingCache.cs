using System;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;

namespace Hasin.Taaghche.Utilities
{
    public class AutoRefreshingCache<T> where T : class 
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
            var item = Get(key);
            if (item == null)
            {
                Refresh(key, null, calc);
                return Get(key).Value;
            }

            var refreshThreshold = DateTime.UtcNow.AddSeconds(-_refreshAfterSeconds);
            if (item.CalculationTime < refreshThreshold)
            {
                if (Interlocked.Increment(ref item.RefreshWorkers) == 1)
                {
                    Task.Run(() =>
                    {
                        Refresh(key, item, calc);
                    });
                }
            }
            return item.Value;
        }

        private void Refresh(string key, CacheItemHolder dataRefreshLock, Func<T> calc)
        {
            var data = calc();
            var absoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(_expireAfterSeconds);
            lock (_cache)
            {
                _cache.Set(key, new CacheItemHolder(data), absoluteExpiration);
            }
            if (dataRefreshLock != null)
            {
                Interlocked.Decrement(ref dataRefreshLock.RefreshWorkers);
            }
        }

        private CacheItemHolder Get(string key)
        {
            return _cache.Get(key) as CacheItemHolder;
        }

        class CacheItemHolder
        {
            internal readonly T Value;
            internal Int32 RefreshWorkers;
            internal readonly DateTime CalculationTime;

            public CacheItemHolder(T value)
            {
                Value = value;
                RefreshWorkers = 0;
                CalculationTime = DateTime.UtcNow;
            }
        }
    }
    
}
