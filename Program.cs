using DiscordMusicBot.Bot;
using DiscordMusicBot.Music;
using DiscordMusicBot.Web;

class Program
{
    static async Task Main(string[] args)
    {
        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("❌ DISCORD_TOKEN not found");
            return;
        }

        var music = new MusicService();

        // 🌐 Web ต้องรัน
        _ = WebServer.StartAsync(music);

        // 🤖 Discord Bot
        var bot = new BotService(token, music);
        await bot.StartAsync();

        await Task.Delay(-1);
    }
}

