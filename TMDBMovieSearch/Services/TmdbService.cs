using Newtonsoft.Json.Linq;
using System;
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

        private readonly Dictionary<string, CacheEntry> _cache = new();

        private readonly object _cacheLock = new object();
        private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(1);

        private static readonly object _consoleLock = new object();

        private void SafeWriteLine(string message)
        {
            lock(_consoleLock)
            {
                Console.WriteLine(message);
            }
        }

        public TmdbService(string baseUrl, string apiKey, HttpClient client)
        {
            _baseUrl = baseUrl;
            _apiKey = apiKey;
            _client = client;
        }

        public JObject Search(string query, Dictionary<string, string>? extraParams = null)
        {
            string cacheKey = GenerateCacheKey(query, extraParams);
            Stopwatch stopwatch = Stopwatch.StartNew();

            if (_cache.TryGetValue(cacheKey, out CacheEntry? entry ) && !entry.IsExpired)
            {
                stopwatch.Stop();
                SafeWriteLine($"[CACHE HIT] '{query}' -> {stopwatch.Elapsed}s");
                return entry.Value;
            }

            //nije u kesu
            lock(_cacheLock)
            {
                if (_cache.TryGetValue(cacheKey, out CacheEntry? entryInner))
                {
                    if (entryInner.IsExpired)
                    {
                        _cache.Remove(cacheKey);
                        SafeWriteLine($"[CACHE EXPIRED] '{query}' -> uklonjen iz kesa");
                        PrintCacheStatsInternal();
                    }

                    else
                    {
                        stopwatch.Stop();
                        SafeWriteLine($"[CACHE HIT] '{query}' -> {stopwatch.Elapsed}s ");
                        return entryInner.Value;
                    }
                }

                //sigurno nema
                SafeWriteLine($"[CACHE MISS] '{query}' -> pozivam TMDB API...");

                try
                {
                    JObject result = CallApi(query, extraParams);
                    //ne kesiramo ako nema rezultata
                    JArray? movies = result["results"] as JArray;
                    if (movies != null && movies.Count > 0)
                    {
                        _cache[cacheKey] = new CacheEntry(result, _cacheTtl);
                        stopwatch.Stop();
                       SafeWriteLine($"[CACHED] '{query}' -> {stopwatch.Elapsed}s");
                        PrintCacheStatsInternal();
                    }
                    else
                    {
                        stopwatch.Stop();
                        SafeWriteLine($"[NOT CACHED] '{query}' -> nema rezultata");
                    }

                    return result;
                }
                catch(Exception e)
                {
                    stopwatch.Stop();
                    SafeWriteLine($"[ERROR]\t\t'{query}' -> {e.Message}");
                    throw;
                }
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

        private JObject CallApi(string query, Dictionary<string, string>? extraParams = null)
        {
            string url = $"{_baseUrl}?query={Uri.EscapeDataString(query)}&api_key={_apiKey}";

            if (extraParams != null)
                foreach (var kvp in extraParams)
                    url += $"&{kvp.Key}={Uri.EscapeDataString(kvp.Value)}";

            HttpResponseMessage response = _client.GetAsync(url).Result;
            response.EnsureSuccessStatusCode();

            string body = response.Content.ReadAsStringAsync().Result;
            return JObject.Parse(body);
        }

        private void PrintCacheStatsInternal()
        {
            var expiredKeys = _cache
                        .Where(p => p.Value.IsExpired)
                        .Select(p => p.Key)
                        .ToList();

            foreach (var key in expiredKeys)
                _cache.Remove(key);

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

            lock (_consoleLock)
            {
                Console.Write(sb.ToString());
            }
        }

        public void PrintCacheStats()
        {
            lock(_cacheLock)
            {

                var expiredKeys = _cache
                        .Where(p => p.Value.IsExpired)
                        .Select(p => p.Key)
                        .ToList();

                foreach (var key in expiredKeys)
                    _cache.Remove(key);

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

                lock(_consoleLock)
                {
                    Console.Write(sb.ToString());
                }
            }
        }

    }
}
