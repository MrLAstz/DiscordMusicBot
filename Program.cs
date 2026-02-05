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

        // ✅ แก้ไข: เพิ่ม Config เพื่อให้บอทมองเห็นสมาชิกและสถานะออนไลน์
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged |
                             GatewayIntents.GuildPresences |
                             GatewayIntents.GuildMembers |
                             GatewayIntents.GuildVoiceStates,
            AlwaysDownloadUsers = true, // โหลดสมาชิกทั้งหมดมาไว้ใน Cache
            MessageCacheSize = 100
        };

        _client = new DiscordSocketClient(config);
    }

    public async Task StartAsync()
    {
        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();

        _client.Ready += () =>
        {
            Console.WriteLine($"✅ บอทออนไลน์แล้ว: {_client.CurrentUser.Username}");
            return Task.CompletedTask;
        };
    }
}