using DiscordMusicBot.Bot;
using DiscordMusicBot.Music;
using DiscordMusicBot.Web;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 Starting application");

        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("❌ DISCORD_TOKEN not found");
            return;
        }

        var music = new MusicService();

        // 🌐 Web (ต้องขึ้นก่อน)
        _ = Task.Run(() => WebServer.StartAsync(args, music));

        // 🤖 Bot
        var bot = new BotService(token, music);
        await bot.StartAsync();

        Console.WriteLine("✅ Bot + Web started");

        await Task.Delay(-1);
    }
}
