using Discord;           // สำหรับ GatewayIntents
using Discord.WebSocket; // สำหรับ DiscordSocketConfig และ DiscordSocketClient
using DiscordMusicBot.Bot;
using DiscordMusicBot.Music;
using DiscordMusicBot.Web;

class Program
{
    static async Task Main(string[] args)
    {
        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("❌ DISCORD_TOKEN not found");
            return;
        }

        // 1. สร้าง Service หลัก
        var music = new MusicService();

        // 2. ส่งแค่ Token และ Service เข้าไป (ไม่ต้องสร้าง Client ตรงนี้)
        var bot = new BotService(token, music);

        // 3. เริ่ม WebServer
        _ = Task.Run(() => WebServer.Start(args, music, port));

        // 4. เริ่มบอท
        await bot.StartAsync();

        await Task.Delay(-1);
    }
}