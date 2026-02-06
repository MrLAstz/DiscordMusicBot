using DiscordMusicBot.Bot;
using DiscordMusicBot.Music;
using DiscordMusicBot.Web;
using System.Runtime.InteropServices;

class Program
{
    static async Task Main(string[] args)
    {
        // โหลด Library เสียงสำหรับ Linux บน Railway
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            NativeLibrary.TryLoad("libopus", out _);
            NativeLibrary.TryLoad("libsodium", out _);
            Console.WriteLine("🐧 Audio libraries pre-loaded for Linux.");
        }

        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("❌ DISCORD_TOKEN not found");
            return;
        }

        var music = new MusicService();

        _ = Task.Run(() => WebServer.Start(args, music, port));

        var bot = new BotService(token, music);
        await bot.StartAsync();

        await Task.Delay(-1);
    }
}