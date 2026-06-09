using TMDBMovieSearch.Server;
using DotNetEnv;

    
class Program
{
    static async Task Main(string[] args)
    {
        Env.Load();

        string? apiKey = Environment.GetEnvironmentVariable("API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: API_KEY nije pronadjen u .env fajlu!");
            return;
        }

        var server = new WebServer("http://localhost:5000/", apiKey);
        server.Start();
    }
}
