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

        var music = new MusicService();
        var bot = new BotService(token, music);

        // 🔥 1. start bot
        await bot.StartAsync();

        // 🔥 2. รอ Discord READY (สำคัญที่สุด)
        await bot.WaitUntilReadyAsync();

        // 🔥 3. ค่อยเปิด Web API
        _ = Task.Run(() => WebServer.Start(args, music, port));

        Console.WriteLine("🌐 Web server started");

        await Task.Delay(-1);
    }
}
