using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using DiscordMusicBot.Music;
using Microsoft.AspNetCore.Http;

namespace DiscordMusicBot.Web;

public static class WebServer
{
    public static void Start(string[] args, MusicService music, string port)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton(music);
        builder.WebHost.UseUrls($"http://*:{port}");

        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        // ✅ 1. สำหรับกดปุ่ม Join บนหน้าเว็บ
        app.MapPost("/join", async () =>
        {
            await music.JoinLastAsync();
            return Results.Ok(new { message = "Joined" });
        });

        // ✅ 2. สำหรับกดเล่นเพลง (รับ URL จาก Query String เช่น /play?url=...)
        app.MapPost("/play", async (string url) =>
        {
            if (string.IsNullOrEmpty(url)) return Results.BadRequest("URL is required");
            await music.PlayLastAsync(url);
            return Results.Ok(new { message = "Playing" });
        });

        // ✅ 3. สำหรับดึงข้อมูลชื่อ Server และรายชื่อสมาชิก (Live Data)
        app.MapGet("/status", () =>
        {
            var statusData = music.GetUsersInVoice();
            return Results.Ok(statusData);
        });

        app.Run();
    }
}