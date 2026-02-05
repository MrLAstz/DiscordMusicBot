using DiscordMusicBot.Music;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusicBot.Web;

public static class WebServer
{
    public static void Start(string[] args, MusicService music)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton(music);

        var app = builder.Build();

        // 👉 เปิด static files (wwwroot)
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // 👉 API: join voice
        app.MapPost("/join", async (MusicService music) =>
        {
            await music.JoinLastAsync();
            return Results.Ok();
        });

        // 👉 API: play
        app.MapPost("/play", async (string url, MusicService music) =>
        {
            await music.PlayLastAsync(url);
            return Results.Ok();
        });

        app.Run();
    }
}
