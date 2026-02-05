using DiscordMusicBot.Bot;
using DiscordMusicBot.Music;
using DiscordMusicBot.Web;

class Program
{
    static async Task Main(string[] args)
    {
        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        // Railway จะส่งพอร์ตมาให้ผ่านตัวแปร PORT
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("❌ DISCORD_TOKEN not found");
            return;
        }
        var config = new DiscordSocketConfig
        {
            // หัวใจสำคัญคือบรรทัดนี้ครับ เพื่อให้บอทมองเห็นว่าใครเข้า/ออกห้อง Voice
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildPresences | GatewayIntents.GuildMembers
        };

        _client = new DiscordSocketClient(config);
        var music = new MusicService();

        // ▶️ เปิดเว็บ (ส่ง port เข้าไปด้วย)
        _ = Task.Run(() => WebServer.Start(args, music, port));

        // ▶️ เปิดบอท
        var bot = new BotService(token, music);
        await bot.StartAsync();

        await Task.Delay(-1);
    }
}