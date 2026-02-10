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
        var config = new DiscordSocketConfig
        {
            // ... ของเดิมที่คุณมี ...
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildVoiceStates, // ต้องมีตัวนี้!
            AlwaysDownloadUsers = true
        };
        var client = new DiscordSocketClient(config);
        var music = new MusicService();
        _ = Task.Run(() => WebServer.Start(args, music, port));

        var bot = new BotService(token, music);
        await bot.StartAsync();

        await Task.Delay(-1);
    }
}