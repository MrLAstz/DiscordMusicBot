using DiscordMusicBot.Bot;
using Microsoft.Extensions.Configuration;

class Program
{
    static async Task Main(string[] args)
    {
        // โหลด appsettings.json
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // ดึง Token
        var token = config["Discord:Token"];

        // เช็กกันพัง
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("❌ ไม่พบ Discord Token ใน appsettings.json");
            return;
        }

        // สร้างบอท
        var bot = new BotService(token);
        await bot.StartAsync();

        // กันโปรแกรมปิด
        await Task.Delay(-1);
    }
}
