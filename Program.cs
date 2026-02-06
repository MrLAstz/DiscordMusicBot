using DiscordMusicBot.Bot;
using DiscordMusicBot.Music;
using DiscordMusicBot.Web;
using System.Runtime.InteropServices;

class Program
{
    static async Task Main(string[] args)
    {
        // ปล่อยให้ Runtime จัดการ Library เอง เราแค่ดึง Token และ Port มาทำงาน
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