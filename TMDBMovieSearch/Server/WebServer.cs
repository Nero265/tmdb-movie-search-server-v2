using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TMDBMovieSearch.Services;

namespace TMDBMovieSearch.Server
{
    public class WebServer
    {
        private readonly HttpListener _listener;
        private readonly string _prefix;
        private readonly TmdbService _tmdbService;
        private readonly SemaphoreSlim _requestThrottle;
        private const int MaxConcurrentRequest = 10;

        public WebServer(string prefix, string apiKey)
        {
            _prefix = prefix;
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _requestThrottle = new SemaphoreSlim(MaxConcurrentRequest, MaxConcurrentRequest);
            _tmdbService = new TmdbService(
                baseUrl: "https://api.themoviedb.org/3/search/movie",
                apiKey: apiKey,
                client: new HttpClient()
            );
        }

        public void Start()
        {
            _listener.Start();
            Console.WriteLine($"Server slusa na {_prefix}");

            // petlja za osluskivanje 
            Thread listenerThread = new Thread(() =>
            {
                while (_listener.IsListening)
                {
                    try
                    {
                        HttpListenerContext context = _listener.GetContext();
                        _ = Task.Run(() => HandleRequest(context));
                    }
                    catch (HttpListenerException) when (!_listener.IsListening)
                    {
                        break;
                    }
                }
            });

            listenerThread.IsBackground = true; ;
            listenerThread.Start();

            Console.WriteLine("Press ENTER to stop the server...");
            Console.ReadLine();

            Stop();
        }

        public void Stop()
        {
            _listener.Stop();
            Console.WriteLine("Server je zaustavljen.");
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            // Ignorisemo favicon.ico zahteve
            if (context.Request.Url!.AbsolutePath == "/favicon.ico")
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            Console.WriteLine($"[REQUEST] {context.Request.HttpMethod} {context.Request.Url}");

            string? query = context.Request.QueryString["query"]; //citamo query parametar
            var extraParams = new Dictionary<string, string>(); //moguci filteri
            foreach (string? key in context.Request.QueryString.AllKeys)
            {
                if (key != null && key != "query")
                {
                    extraParams[key] = context.Request.QueryString[key]!;
                }
            }

            if (string.IsNullOrEmpty(query))
            {
                await SendResponseAsync(context, 400, "Nedostaje query parametar. Primer: /search?query=Project+Hail+Mary");
                return;
            }

            await _requestThrottle.WaitAsync();
            try
            {
                JObject result = await _tmdbService.SearchAsync(query, extraParams);
                JArray movies = (JArray)result["results"]!;
                //_tmdbService.PrintCacheStats();
                if (movies.Count == 0)
                {
                    await SendResponseAsync(context, 404, "Nisu pronadjeni filmovi za dati upit");
                }
                else
                {
                    await SendResponseAsync(context, 200, result.ToString());
                }

            }
            catch (Exception e)
            {
                await SendResponseAsync(context, 500, $"Error while calling TMDB API: {e.Message}");
            }
            finally
            {
                _requestThrottle.Release();
            }
        }

        private async Task SendResponseAsync(HttpListenerContext context, int statusCode, string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message); //HTTP salje bytes

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;

            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }
    }
}
