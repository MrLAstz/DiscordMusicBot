namespace DiscordMusicBot.Web;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using DiscordMusicBot.Music;

public static class WebServer
{
    public static void Start(string[] args, MusicService music, string port)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ✅ 1. ลงทะเบียน Service ให้ระบบรู้จัก (เพื่อความเสถียร)
        builder.Services.AddSingleton(music);

        // บังคับพอร์ตสำหรับ Railway
        builder.WebHost.UseUrls($"http://*:{port}");

        var app = builder.Build();

        // ✅ 2. เปิดใช้งานการอ่านไฟล์ index.html จาก wwwroot
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

        // ✅ 3. ใช้ MapGet อันเดียวที่ส่งข้อมูลครบๆ (รวมร่างแล้ว)
        app.MapGet("/status", () =>
        {
            return Results.Ok(new
            {
                guild = music.CurrentGuildName,
                users = music.GetUsersInVoice() // ดึงรายชื่อคนในห้อง
            });
        });

        app.Run();
    }
}