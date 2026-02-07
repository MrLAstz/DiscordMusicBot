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

        // ลงทะเบียน Services
        builder.Services.AddSingleton(music);
        builder.Services.AddSingleton<YoutubeService>(); // ✅ ลงทะเบียน YoutubeService ให้ระบบรู้จัก
        builder.Services.AddControllers(); // ✅ ลงทะเบียน Controller Service

        builder.Services.AddCors(options => {
            options.AddDefaultPolicy(policy => {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            });
        });

        builder.WebHost.UseUrls($"http://*:{port}");
        var app = builder.Build();

        app.UseCors();
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // --- 🔍 แก้ไข API Search ให้รับค่า offset เพื่อทำ Infinite Scroll ---
        app.MapGet("/api/search", async (string q, int? offset, YoutubeService yt) =>
        {
            if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest();
            try
            {
                // ส่ง offset (จุดเริ่มต้นค้นหา) เข้าไปใน Method (เริ่มต้นที่ 0 ถ้าไม่มีการส่งมา)
                var results = await yt.SearchVideosAsync(q, 18, offset ?? 0);
                return Results.Ok(results);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        // --- Endpoint เดิมของคุณ ---
        app.MapGet("/status", async (HttpContext context, MusicService musicService) =>
        {
            string? userIdStr = context.Request.Query["userId"];
            if (ulong.TryParse(userIdStr, out ulong userId))
            {
                var statusData = await musicService.GetUsersInVoice(userId);
                return Results.Ok(statusData);
            }
            return Results.Ok(new { guild = "กรุณา Login", users = new List<object>() });
        });

        app.MapPost("/join", async (HttpContext context, MusicService musicService) =>
        {
            string? userIdStr = context.Request.Query["userId"];
            if (ulong.TryParse(userIdStr, out ulong userId))
            {
                bool success = await musicService.JoinByUserIdAsync(userId);
                return success ? Results.Ok(new { message = "Joined user's channel" })
                               : Results.BadRequest(new { message = "User not found" });
            }
            return Results.BadRequest(new { message = "User ID is required" });
        });

        app.MapPost("/play", async (HttpContext context, MusicService musicService) =>
        {
            string? url = context.Request.Query["url"];
            string? userIdStr = context.Request.Query["userId"];

            if (string.IsNullOrWhiteSpace(url))
                return Results.BadRequest(new { message = "URL is required" });

            if (!ulong.TryParse(userIdStr, out ulong userId))
                return Results.BadRequest(new { message = "User ID is required" });

            await musicService.EnqueueAsync(
                userId,
                url,
                "web"
            );

            return Results.Ok(new { message = "Added to queue" });
        });



        // --- แก้ไข Endpoint /toggle ---
        app.MapPost("/toggle", async (HttpContext context, MusicService musicService) => {
            string? userIdStr = context.Request.Query["userId"]; // ดึงจาก Query String ให้เหมือน /play
            if (ulong.TryParse(userIdStr, out ulong id))
            {
                await musicService.ToggleAsync(id);
                return Results.Ok(new { message = "Toggled" });
            }
            return Results.BadRequest(new { message = "Invalid User ID" });
        });

        // --- แก้ไข Endpoint /skip ---
        app.MapPost("/skip", async (HttpContext context, MusicService musicService) => {
            string? userIdStr = context.Request.Query["userId"]; // ดึงจาก Query String
            if (ulong.TryParse(userIdStr, out ulong id))
            {
                await musicService.SkipAsync(id);
                return Results.Ok(new { message = "Skipped" });
            }
            return Results.BadRequest(new { message = "Invalid User ID" });
        });
        app.MapControllers();
        app.Run();
    }
}