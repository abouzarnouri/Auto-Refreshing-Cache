using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;

namespace Hasin.Taaghche.Utilities
{
    public class AutoRefreshingCache<T>
    {
        private readonly MemoryCache _cache;
        private readonly int _expireAfterSeconds;
        private readonly int _refreshAfterSeconds;
        public readonly string CacheName;

        // cacheName should be unique in the project and not containing # char
        public AutoRefreshingCache(int expireAfterSeconds, int refreshAfterSeconds, string cacheName)
        {
            _cache = MemoryCache.Default;
            _expireAfterSeconds = expireAfterSeconds;
            _refreshAfterSeconds = refreshAfterSeconds;
            CacheName = cacheName;
        }

        // key, should not contain # char
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
                if (Interlocked.Increment(ref item.RefreshWorkers) == 1)
                {
                    Task.Run(() =>
                    {
                        Refresh(key, calc);
                    });
                }
            }
            return item.Value;
        }

        public int CountExpiredElements(IEnumerable<string> keys)
        {
            return keys?.Where(k => Get(k) == null).Count() ?? 0;
        }

        private void Refresh(string key, Func<T> calc)
        {
            T data;
            try
            {
                data = calc();
            }
            catch (Exception ex)
            {
                data = default(T);
            }
            Inject(key, data);
        }

        public void Inject(string key, T data)
        {
            var absoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(_expireAfterSeconds);
            lock (_cache)
            {
                _cache.Set($"{CacheName}#{key}", new CacheItemHolder(data), absoluteExpiration);
            }
        }

        private CacheItemHolder Get(string key)
        {
            return _cache.Get($"{CacheName}#{key}") as CacheItemHolder;
        }

        private class CacheItemHolder
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
