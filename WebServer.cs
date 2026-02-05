using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using DiscordMusicBot.Music;

namespace DiscordMusicBot.Web;

public static class WebServer
{
    public static async Task StartAsync(MusicService music)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://0.0.0.0:{GetPort()}");

        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapPost("/join", async () =>
        {
            await music.JoinFirstAvailableAsync();
            return Results.Ok();
        });

        app.MapPost("/play", async (string url) =>
        {
            await music.PlayFromUrlAsync(url);
            return Results.Ok();
        });

        Console.WriteLine($"🌐 Web listening on port {GetPort()}");
        await app.RunAsync();
    }

    private static string GetPort()
        => Environment.GetEnvironmentVariable("PORT") ?? "8080";
}
