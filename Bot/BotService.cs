using Discord;
using Discord.WebSocket;
using DiscordMusicBot.Music;

namespace DiscordMusicBot.Bot;

public class BotService
{
    private readonly DiscordSocketClient _client;
    private readonly MusicService _music;
    private readonly string _token;

    public BotService(string token, MusicService music)
    {
        _token = token;
        _music = music;

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.All
        });

        new CommandHandler(_client, _music);
    }

    public async Task StartAsync()
    {
        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();

        Console.WriteLine("🤖 Discord Bot started");
    }
}
