using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using DiscordMusicBot.Music;

namespace DiscordMusicBot.Web;

public static class WebServer
{
    public static async Task StartAsync(MusicService music)
    {
        var builder = WebApplication.CreateBuilder();

        var app = builder.Build();

        app.MapGet("/", () => "🎵 Discord Music Bot Web is running");

        app.MapPost("/join", async () =>
        {
            await music.JoinLastChannelAsync();
            return Results.Ok("joined");
        });

        app.MapPost("/play", async (string url) =>
        {
            await music.PlayFromUrlAsync(url);
            return Results.Ok("playing");
        });

        var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
        Console.WriteLine($"🌐 Web listening on {port}");

        await app.RunAsync($"http://0.0.0.0:{port}");
    }
}
