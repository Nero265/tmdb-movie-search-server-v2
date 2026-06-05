# TMDB Movie Search Server

Concurrent C# web server for searching movies via TMDB API with thread-safe caching and thread synchronization.

## Project Description

A server application that:
- Enables clients to search for movies through a web browser (GET requests)
- Uses TMDB API to fetch movie data
- Implements thread-safe caching to reduce API calls
- Processes multiple requests concurrently using ThreadPool
- Maintains thread-safe logging and error handling

## Example Request

http://localhost:5000/search?query=Avatar


## Requirements

- .NET 6.0 or higher
- TMDB API key (free at https://www.themoviedb.org/settings/api)

## Installation

### 1. Clone the repository
```bash
git clone https://github.com/Nero265/tmdb-movie-search-server.git
cd tmdb-movie-search-server
```

### 2. Configure API key
This project expects the TMDB API key to be provided via environment variables.

Using .env file
Create a .env file in the project root and add:
```bash
API_KEY=your_real_api_key_here
```
The server loads this file at startup (via DotNetEnv library) and makes the variable available through:
```bash
DotNetEnv.Env.Load();
var apiKey = Environment.GetEnvironmentVariable("API_KEY");
```
### 3. Run the server
```bash
dotnet run
```
Server will start on http://localhost:5000

## Features
- Concurrent requests
- Caching: 5-minute TTL, thread-safe dictionary
- Logging: request/response times, cache hits/misses
- Error handling: invalid queries, API failures

## Stress Testing
A Python script (stress_test.py) is included to simulate multiple concurrent requests:
```bash
python stress_test.py
```
It reports:
- Successful requests
- Errors
- Total elapsed time

## Example Output
```bash
[OK] 'Inception' -> 0.523s (status: 200)
[ERROR] 'NonexistentMovie' -> HTTP Error 404: Not Found

======== Results ========
  Success : 34
  Errors  : 1
  Total   : 35
  Time    : 25.9s
=========================
```
## License
MIT License
