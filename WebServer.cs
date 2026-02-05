namespace DiscordMusicBot.Web;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using DiscordMusicBot.Music;

public static class WebServer
{
    public static void Start(string[] args, MusicService music, string port)
    {
        var builder = WebApplication.CreateBuilder(args);

        // บังคับให้ Kestrel รันบนพอร์ตที่ Railway กำหนด
        builder.WebHost.UseUrls($"http://*:{port}");

        var app = builder.Build();

        // ให้แสดงไฟล์ index.html จากโฟลเดอร์ wwwroot
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // API สำหรับหน้าเว็บเรียกใช้
        app.MapPost("/join", async () =>
        {
            await music.JoinLastAsync(); // ใช้ Method ที่คุณมี
            return Results.Ok(new { message = "Joined" });
        });

        app.MapPost("/play", async (string url) =>
        {
            // ตรงนี้ถ้า url มาเป็นคำค้นหา ต้องไปผ่าน YoutubeService ก่อน
            await music.PlayLastAsync(url);
            return Results.Ok(new { message = "Playing" });
        });

        app.MapGet("/status", (MusicService music) =>
        {
            return Results.Ok(new { guild = music.CurrentGuildName });
        });
        app.Run();
    }
}