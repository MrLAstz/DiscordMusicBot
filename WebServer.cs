using DiscordMusicBot.Music;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;

namespace DiscordMusicBot.Web;

public static class WebServer
{
    public static async Task StartAsync(string[] args, MusicService music)
    {
        try
        {
            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            Console.WriteLine($"🌐 PORT = {port}");

            var builder = WebApplication.CreateBuilder(args);

            // ✅ ชี้ไปที่ wwwroot
            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

            var app = builder.Build();

            // ✅ เปิด static files
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")),
                RequestPath = ""
            });

            // health check
            app.MapGet("/health", () => "OK");

            Console.WriteLine("✅ Web server started");
            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("🔥 WebServer crashed");
            Console.WriteLine(ex);
            throw;
        }
    }
}
