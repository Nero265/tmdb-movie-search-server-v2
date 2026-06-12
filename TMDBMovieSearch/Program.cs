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

        var tcs = new TaskCompletionSource();
        _ = Task.Run(() =>
        {
            Console.ReadLine();
            tcs.SetResult();
        });

        _ = server.StartAsync();

        Console.WriteLine("Press ENTER to stop the server...");
        await tcs.Task;

        server.Stop();
    }
}
