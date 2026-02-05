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

        // 🌐 เริ่ม Web Server (ต้องอยู่ foreground)
        var webTask = WebServer.StartAsync(music);

        // 🤖 เริ่ม Discord Bot
        var bot = new BotService(token, music);
        await bot.StartAsync();

        Console.WriteLine("✅ Bot + Web Server running");

        await Task.WhenAll(webTask);
    }
}
