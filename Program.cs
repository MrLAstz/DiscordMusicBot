using DiscordMusicBot.Bot;
using DiscordMusicBot.Music;
using DiscordMusicBot.Web;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 Starting app");

        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("❌ DISCORD_TOKEN not found");
            return;
        }

        var music = new MusicService();

        // 🌐 Web ต้องรันแบบ background
        _ = Task.Run(() => WebServer.StartAsync(args, music));

        // 🤖 Discord Bot
        var bot = new BotService(token, music);
        await bot.StartAsync();

        Console.WriteLine("✅ Bot + Web running");
        await Task.Delay(-1);
    }
}
