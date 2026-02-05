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

        // ✅ 1. เพิ่มนโยบาย CORS เพื่อแก้ปัญหา 403 Forbidden และ CORB
        builder.Services.AddCors(options => {
            options.AddDefaultPolicy(policy => {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            });
        });

        builder.WebHost.UseUrls($"http://*:{port}");

        var app = builder.Build();

        // ✅ 2. ลำดับการวาง Middleware สำคัญมาก
        app.UseCors(); // ต้องอยู่ก่อน StaticFiles
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // ✅ 3. Endpoint สำหรับเช็คสถานะและสมาชิกในห้อง
        app.MapGet("/status", (HttpContext context) =>
        {
            string? userIdStr = context.Request.Query["userId"];
            if (ulong.TryParse(userIdStr, out ulong userId))
            {
                // จะดึงสมาชิกเฉพาะในห้อง Lobby หรือห้องที่คุณอยู่จริงๆ
                var statusData = music.GetUsersInVoice(userId);
                return Results.Ok(statusData);
            }
            return Results.Ok(new { guild = "กรุณา Login", users = new List<object>() });
        });

        // ✅ 4. Endpoint สำหรับ Join
        app.MapPost("/join", async (HttpContext context) =>
        {
            string? userIdStr = context.Request.Query["userId"];
            if (ulong.TryParse(userIdStr, out ulong userId))
            {
                bool success = await music.JoinByUserIdAsync(userId);
                return success ? Results.Ok(new { message = "Joined user's channel" })
                               : Results.BadRequest(new { message = "User not found" });
            }
            await music.JoinLastAsync();
            return Results.Ok(new { message = "Joined fallback" });
        });

        // ✅ 5. Endpoint สำหรับ Play
        app.MapPost("/play", async (HttpContext context) =>
        {
            string? url = context.Request.Query["url"];
            string? userIdStr = context.Request.Query["userId"];
            if (string.IsNullOrEmpty(url)) return Results.BadRequest("URL is required");

            if (ulong.TryParse(userIdStr, out ulong userId))
            {
                await music.PlayByUserIdAsync(userId, url);
                return Results.Ok(new { message = "Playing for user" });
            }
            await music.PlayLastAsync(url);
            return Results.Ok(new { message = "Playing last" });
        });

        app.Run(); // รันแค่ครั้งเดียวพอครับ
    }
}