using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using DiscordMusicBot.Music;

namespace DiscordMusicBot.Web;

public static class WebServer
{
    public static async Task StartAsync(string[] args, MusicService music)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        app.MapGet("/", () => "🎵 Discord Music Bot is running");

        app.MapPost("/join", async () =>
        {
            await music.JoinFromWebAsync();
            return Results.Ok("joined");
        });

        app.MapPost("/play", async (string url) =>
        {
            await music.PlayFromWebAsync(url);
            return Results.Ok("playing");
        });

        // ⭐ Railway PORT
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
        Console.WriteLine($"🌐 Web listening on port {port}");

        await app.RunAsync($"http://0.0.0.0:{port}");
    }
}