using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using DiscordMusicBot.Music;

namespace DiscordMusicBot.Web;

public static class WebServer
{
    public static async Task StartAsync(string[] args, MusicService music)
    {
        try
        {
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();

            app.MapGet("/", () => "✅ Web server is running");

            var port = Environment.GetEnvironmentVariable("PORT");
            if (string.IsNullOrEmpty(port))
            {
                Console.WriteLine("❌ PORT not found, defaulting to 8080");
                port = "8080";
            }

            Console.WriteLine($"🌐 Listening on 0.0.0.0:{port}");

            await app.RunAsync($"http://0.0.0.0:{port}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("🔥 WebServer crashed");
            Console.WriteLine(ex);
            throw;
        }
    }
}