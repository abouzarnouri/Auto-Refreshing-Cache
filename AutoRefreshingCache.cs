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
                Refresh(key, calc);
                return Get(key).Value;
            }

            var refreshThreshold = DateTime.UtcNow.AddSeconds(-_refreshAfterSeconds);
            if (item.CalculationTime < refreshThreshold)
            {
                if (Interlocked.Exchange(ref item.RefreshWorkers, 1) == 0)
                {
                    Task.Run(() =>
                    {
                        Refresh(key, calc);
                    });
                }
            }
            return item.Value;
        }

        private void Refresh(string key, Func<T> calc)
        {
            var data = calc();
            var absoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(_expireAfterSeconds);
            lock (_cache)
            {
                _cache.Set(key, new CacheItemHolder(data), absoluteExpiration);
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
