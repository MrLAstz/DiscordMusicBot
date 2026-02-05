namespace DiscordMusicBot.Web;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using DiscordMusicBot.Music;

public static class WebServer
{
    public static void Start(string[] args, MusicService music, string port)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ✅ 1. ต้องเพิ่มบรรทัดนี้ เพื่อให้ API รู้จักตัวแปร music
        builder.Services.AddSingleton(music);

        builder.WebHost.UseUrls($"http://*:{port}");

        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapPost("/join", async () =>
        {
            await music.JoinLastAsync();
            return Results.Ok(new { message = "Joined" });
        });

        app.MapPost("/play", async (string url) =>
        {
            await music.PlayLastAsync(url);
            return Results.Ok(new { message = "Playing" });
        });

        // ✅ 2. แก้ตรงนี้ให้รับ music เข้ามาให้ถูกทาง (หรือจะใช้ music ตัวนอกเลยก็ได้)
        app.MapGet("/status", () =>
        {
            return Results.Ok(new { guild = music.CurrentGuildName });
        });

        app.Run();
    }
}