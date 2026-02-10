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
            // บังคับให้เริ่ม Session ใหม่เสมอถ้าอันเก่ามีปัญหา
            AlwaysDownloadUsers = false,
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildVoiceStates,
            // เพิ่มบรรทัดนี้:
            HandlerTimeout = 30000,
        };
        var client = new DiscordSocketClient(config);
        var music = new MusicService();
        _ = Task.Run(() => WebServer.Start(args, music, port));

        var bot = new BotService(token, music);
        await bot.StartAsync();

        await Task.Delay(-1);
    }
}