using Discord;
using Discord.WebSocket;

namespace DiscordMusicBot.Bot;

public class BotService
{
    private readonly string _token;
    private DiscordSocketClient? _client;

    public BotService(string token)
    {
        _token = token;
    }

    public async Task StartAsync()
    {
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents =
                GatewayIntents.Guilds |
                GatewayIntents.GuildVoiceStates |
                GatewayIntents.GuildMessages |
                GatewayIntents.MessageContent
        });

        _client.Log += msg =>
        {
            Console.WriteLine(msg);
            return Task.CompletedTask;
        };

        // 👇 ส่ง client ให้ command
        new CommandHandler(_client);

        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();
    }
}
