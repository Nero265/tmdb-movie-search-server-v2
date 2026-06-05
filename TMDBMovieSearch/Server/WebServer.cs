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

        //nakon tmdbservice
        private readonly TmdbService _tmdbService;

        public WebServer(string prefix, string apiKey)
        {
            _prefix = prefix;
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
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
                    HttpListenerContext context = _listener.GetContext();

                    HttpListenerContext capturedContext = context; //captured-variable

                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        HandleRequest(capturedContext);
                    });
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

        private void HandleRequest(HttpListenerContext context)
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
            foreach(string? key in context.Request.QueryString.AllKeys)
            {
                if (key != null && key != "query")
                {
                    extraParams[key] = context.Request.QueryString[key]!;
                }
            }
            
            if (string.IsNullOrEmpty(query))
            {
                SendResponse(context, 400, "Nedostaje query parametar. Primer: /search?query=Project+Hail+Mary");
                return;
            }

            try
            {
                JObject result = _tmdbService.Search(query, extraParams);
                JArray movies = (JArray)result["results"]!;
                //_tmdbService.PrintCacheStats();
                if (movies.Count == 0)
                {
                    SendResponse(context, 404, "Nisu pronadjeni filmovi za dati upit");
                }
                else
                {
                    SendResponse(context, 200, result.ToString());
                }
                
            }
            catch(Exception e)
            {
                SendResponse(context, 500, $"Error while calling TMDB API: {e.Message}");
            }
        }

        private void SendResponse(HttpListenerContext context, int statusCode, string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message); //HTTP salje bytes

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;

            context.Response.OutputStream.Write(buffer, 0, buffer.Length);

            context.Response.OutputStream.Close();
        }
    }
}
