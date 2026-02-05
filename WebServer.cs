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

        // ✅ 1. แก้ไขส่วน Join: รับ userId จาก Query String
        app.MapPost("/join", async (HttpContext context) =>
        {
            string? userIdStr = context.Request.Query["userId"];

            if (ulong.TryParse(userIdStr, out ulong userId))
            {
                // เรียกใช้ฟังก์ชันใหม่ที่เราเพิ่มใน MusicService
                bool success = await music.JoinByUserIdAsync(userId);
                return success ? Results.Ok(new { message = "Joined user's channel" })
                               : Results.BadRequest(new { message = "User not found in any voice channel" });
            }

            // ถ้าไม่มี userId ส่งมา ให้ใช้ระบบเดิมเป็น fallback
            await music.JoinLastAsync();
            return Results.Ok(new { message = "Joined fallback channel" });
        });

        // ✅ 2. แก้ไขส่วน Play: รับทั้ง url และ userId
        app.MapPost("/play", async (HttpContext context) =>
        {
            string? url = context.Request.Query["url"];
            string? userIdStr = context.Request.Query["userId"];

            if (string.IsNullOrEmpty(url)) return Results.BadRequest("URL is required");

            if (ulong.TryParse(userIdStr, out ulong userId))
            {
                // สั่งเล่นเพลงโดยอ้างอิงจากห้องที่ User อยู่ ณ ตอนนั้น
                await music.PlayByUserIdAsync(userId, url);
                return Results.Ok(new { message = "Playing for user" });
            }

            // ถ้าไม่มี userId ให้เล่นในห้องล่าสุดที่บอทจำได้ (ระบบเดิม)
            await music.PlayLastAsync(url);
            return Results.Ok(new { message = "Playing in last channel" });
        });

        // ✅ 3. ส่วน status คงเดิมไว้เพื่อให้หน้าเว็บดึงข้อมูลได้
        app.MapGet("/status", (HttpContext context) =>
        {
            // ดึง userId จาก Query String ที่ส่งมาจากหน้าเว็บ
            string? userIdStr = context.Request.Query["userId"];

            if (ulong.TryParse(userIdStr, out ulong userId))
            {
                // ส่ง userId เข้าไปในฟังก์ชัน (แก้ Error CS7036)
                var statusData = music.GetUsersInVoice(userId);
                return Results.Ok(statusData);
            }

            // ถ้าไม่มี userId (เช่น ยังไม่ได้ Login) ให้ส่งค่าว่างกลับไป
            return Results.Ok(new { guild = "กรุณา Login", users = new List<object>() });
        });

        app.Run();
    }
}