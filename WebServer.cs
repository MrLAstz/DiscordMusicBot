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

        app.MapGet("/status", async (HttpContext context, MusicService musicService) =>
        {
            string? userIdStr = context.Request.Query["userId"];
            if (ulong.TryParse(userIdStr, out ulong userId))
            {
                // แก้จุดนี้: ใส่ await
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
            await musicService.JoinLastAsync();
            return Results.Ok(new { message = "Joined fallback" });
        });

        app.MapPost("/play", async (HttpContext context, MusicService musicService) =>
        {
            string? url = context.Request.Query["url"];
            string? userIdStr = context.Request.Query["userId"];
            if (string.IsNullOrEmpty(url)) return Results.BadRequest("URL is required");

            if (ulong.TryParse(userIdStr, out ulong userId))
            {
                await musicService.PlayByUserIdAsync(userId, url);
                return Results.Ok(new { message = "Playing for user" });
            }
            await musicService.PlayLastAsync(url);
            return Results.Ok(new { message = "Playing last" });
        });

        app.Run();
    }
}