# TMDB Movie Search Server v2

Concurrent C# web server for searching movies via TMDB API with async/await, 
task-based processing, thread-safe caching and cache stampede prevention.

## Project Description

A refactored version of the original TMDB Movie Search Server, reimplemented 
using modern async/task-based patterns. Key improvements over v1:

- `async/await` + `Task` replaces `ThreadPool.QueueUserWorkItem`
- `GetContextAsync()` replaces blocking listener thread
- `ConcurrentDictionary` replaces `Dictionary` + global lock
- Per-key `SemaphoreSlim` for cache stampede prevention
- `ContinueWith` continuations for post-API processing
- Controlled concurrency via request throttling (`SemaphoreSlim`)
- Thread-safe async logging
- `async Main` with `TaskCompletionSource` for clean shutdown
- Cache size limit (max 10 entries, FIFO eviction)
- Background cleanup task (removes expired entries every 30s)

## Example Request

http://localhost:5000/search?query=Avatar

## Requirements

- .NET 6.0 or higher
- TMDB API key (free at https://www.themoviedb.org/settings/api)

## Installation

### 1. Clone the repository

git clone https://github.com/Nero265/tmdb-movie-search-server-v2.git
cd tmdb-movie-search-server-v2

### 2. Configure API key

Create a .env file in the project root:

API_KEY=your_real_api_key_here

### 3. Run the server

dotnet run

Server will start on http://localhost:5000

## Features

- Async request handling with `async/await`
- Cache stampede prevention via per-key `SemaphoreSlim`
- TTL-based thread-safe caching with `ConcurrentDictionary`
- Request throttling (max 10 concurrent requests)
- Thread-safe async logging
- `ContinueWith` continuations for result processing
- Cache size limit (max 10 entries, FIFO eviction)
- Background cleanup task (removes expired entries every 30s)
- Async listener loop with `GetContextAsync()` (no dedicated listener thread)
- Graceful shutdown via `TaskCompletionSource` (Press ENTER to stop)

## Stress Testing

python stress_test.py

## License

MIT License
