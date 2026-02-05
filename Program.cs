using Microsoft.Extensions.Configuration;
using DiscordMusicBot.Bot;

class Program
{
    static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var token = config["Discord:Token"];

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("❌ Discord Token not found");
            return;
        }

        var bot = new BotService(token);
        await bot.StartAsync();

        await Task.Delay(-1);
    }
}
