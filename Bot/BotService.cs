using Discord;
using Discord.WebSocket;
using DiscordMusicBot.Music;

namespace DiscordMusicBot.Bot;

public class BotService
{
    private readonly DiscordSocketClient _client;
    private readonly string _token;
    private readonly MusicService _music;

    public BotService(string token, MusicService music)
    {
        _token = token;
        _music = music;

        // แก้ไขจุดนี้: ใส่ Config เพื่อเปิดใช้งาน Intents และ Caching
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged |
                             GatewayIntents.GuildMembers |
                             GatewayIntents.GuildPresences,
            AlwaysDownloadUsers = true,
            MessageCacheSize = 100
        };

        _client = new DiscordSocketClient(config);
        _music.SetDiscordClient(_client);
    }

    public async Task StartAsync()
    {
        _client.Log += LogAsync;
        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();
    }

    private Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }
}