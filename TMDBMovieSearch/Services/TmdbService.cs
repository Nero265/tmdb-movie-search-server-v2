using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMDBMovieSearch.Cache;

namespace TMDBMovieSearch.Services
{
    public class TmdbService
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly HttpClient _client;

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();
        private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(1);

        private static readonly SemaphoreSlim _consoleLock = new SemaphoreSlim(1, 1);

        private async Task LogAsync(string message)
        {
            await _consoleLock.WaitAsync();
            try
            {
                Console.WriteLine(message);
            }
            finally
            {
                _consoleLock.Release();
            }
        }

        public TmdbService(string baseUrl, string apiKey, HttpClient client)
        {
            _baseUrl = baseUrl;
            _apiKey = apiKey;
            _client = client;
        }

        public async Task<JObject> SearchAsync(string query, Dictionary<string, string>? extraParams = null)
        {
            string cacheKey = GenerateCacheKey(query, extraParams);
            Stopwatch stopwatch = Stopwatch.StartNew();

            if (_cache.TryGetValue(cacheKey, out CacheEntry? entry) && !entry.IsExpired)
            {
                stopwatch.Stop();
                await LogAsync($"[CACHE HIT] '{query}' -> {stopwatch.Elapsed}s");
                return entry.Value;
            }

            SemaphoreSlim keyLock = _keyLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await keyLock.WaitAsync();

            //nije u kesu
            try
            {
                //drugi task ?
                if (_cache.TryGetValue(cacheKey, out CacheEntry? entryAfterLock))
                {
                    if (!entryAfterLock.IsExpired)
                    {
                        stopwatch.Stop();
                        await LogAsync($"[CACHE HIT] '{query}' -> {stopwatch.Elapsed}");
                        return entryAfterLock.Value;
                    }

                    else
                    {
                        _cache.TryRemove(cacheKey, out _);
                        await LogAsync($"[CACHE EXPIRED] '{query}' -> uklonjen iz kesa");

                    }
                }

                //sigurno nema
                await LogAsync($"[CACHE MISS] '{query}' -> pozivam TMDB API...");

                return await CallApiAsync(query, extraParams)
                    .ContinueWith(async apiTask =>
                    {
                        JObject result = await apiTask;
                        JArray? movies = result["results"] as JArray;
                        if (movies != null && movies.Count > 0)
                        {
                            _cache[cacheKey] = new CacheEntry(result, _cacheTtl);
                            stopwatch.Stop();
                            await LogAsync($"[CACHED] '{query}' -> {stopwatch.Elapsed}s");
                            await PrintCacheStatsAsync();
                        }
                        else
                        {
                            stopwatch.Stop();
                            await LogAsync($"[NOT CACHED] '{query}' -> nema rezultata");
                        }

                        return result;
                    }).Unwrap();
            }
            catch (Exception e)
            {
                stopwatch.Stop();
                await LogAsync($"[ERROR] '{query}' -> {e.Message}");
                throw;
            }
            finally
            {
                keyLock.Release();
            }
        }

        private string GenerateCacheKey(string query, Dictionary<string, string>? extraParams = null)
        {
            var allParams = new Dictionary<string, string>
            {
                { "query", query.Trim().ToLowerInvariant() }
            };

            if (extraParams != null)
            {
                foreach (var kvp in extraParams)
                    allParams[kvp.Key.ToLowerInvariant()] = kvp.Value.ToLowerInvariant();
            }


            //redosled parametra -radi
            var sorted = allParams.OrderBy(p => p.Key);
            return string.Join("&", sorted.Select(p => $"{p.Key}={p.Value}"));
        }

        private async Task<JObject> CallApiAsync(string query, Dictionary<string, string>? extraParams = null)
        {
            string url = $"{_baseUrl}?query={Uri.EscapeDataString(query)}&api_key={_apiKey}";

            if (extraParams != null)
                foreach (var kvp in extraParams)
                    url += $"&{kvp.Key}={Uri.EscapeDataString(kvp.Value)}";

            HttpResponseMessage response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string body = await response.Content.ReadAsStringAsync();
            return JObject.Parse(body);
        }

        private async Task PrintCacheStatsAsync()
        {
            var expiredKeys = _cache
                        .Where(p => p.Value.IsExpired)
                        .Select(p => p.Key)
                        .ToList();

            foreach (var key in expiredKeys)
                _cache.TryRemove(key, out _);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\n======== Cache stanje ========");
            sb.AppendLine($"\t Unosa u kesu: {_cache.Count}");
            sb.AppendLine($"\t TTL: {_cacheTtl.TotalMinutes} minuta");
            sb.AppendLine("\t Unosi:");

            foreach (var p in _cache)
            {
                string status = p.Value.IsExpired ?
                    "ISTEKAO" : $"istice za {(p.Value.ExpiresAt - DateTime.UtcNow).TotalSeconds:F0}s";

                sb.AppendLine($"\t\t [{status}] '{p.Key}'");
            }

            sb.AppendLine("==============================\n");

            await LogAsync(sb.ToString());
        }
    }
}
