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

        // 🌐 เปิด Web Server (ต้องไม่ await)
        _ = Task.Run(() => WebServer.StartAsync(args, music));

        // 🤖 เปิด Discord Bot
        var bot = new BotService(token, music);
        await bot.StartAsync();

        Console.WriteLine("✅ Bot + Web Server running");

        // 🔒 กันโปรแกรมปิด
        await Task.Delay(-1);
    }
}
