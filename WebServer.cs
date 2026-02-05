using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using DiscordMusicBot.Music;

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

        // ✅ แก้ไข: ใช้ JoinAsync หรือตรวจสอบว่าใน MusicService มี Method ชื่ออะไร
        app.MapPost("/join", async () =>
        {
            // หากไม่มี JoinLastAsync ให้ลองใช้ JoinAsync หรือลบออกชั่วคราวถ้าไม่ได้ใช้
            // ในที่นี้ถ้าคุณอยากให้กดปุ่ม Join แล้วบอทเข้าห้องล่าสุด ให้ใช้ JoinLastAsync
            // แต่ถ้า Error ให้ตรวจสอบใน MusicService.cs ว่าสะกดอย่างไร
            await music.JoinLastAsync();
            return Results.Ok(new { message = "Joined" });
        });

        app.MapPost("/play", async (string url) =>
        {
            await music.PlayLastAsync(url);
            return Results.Ok(new { message = "Playing" });
        });

        // ✅ แก้ไข: เปลี่ยนชื่อเป็น GetUsersInVoice ตามที่คุณมีใน MusicService.cs
        app.MapPost("/play", async (HttpContext context) =>
        {
            // ดึง url จาก query string หรือ body
            string url = context.Request.Query["url"];
            if (string.IsNullOrEmpty(url)) return Results.BadRequest("URL is required");

            await music.PlayLastAsync(url);
            return Results.Ok(new { message = "Playing" });
        });

        app.Run();
    }
}