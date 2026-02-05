using Discord;
using Discord.WebSocket;
using DiscordMusicBot.Music;

namespace DiscordMusicBot.Bot;

public class BotService
{
    private readonly DiscordSocketClient _client;
    private readonly string _token;
    private readonly MusicService _music;

    // ✅ 1. เพิ่มการประกาศตัวแปร _handler ตรงนี้ครับ
    private readonly CommandHandler _handler;

    public BotService(string token, MusicService music)
    {
        _token = token;
        _music = music;

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged |
                             GatewayIntents.GuildMembers |
                             GatewayIntents.GuildPresences |
                             GatewayIntents.MessageContent, // สำคัญ: ต้องมีอันนี้บอทถึงจะอ่านข้อความ !join ได้
            AlwaysDownloadUsers = true
        };

        _client = new DiscordSocketClient(config);
        _music.SetDiscordClient(_client);

        // ✅ 2. สร้าง Instance ของ CommandHandler เพื่อให้ระบบคำสั่งเริ่มทำงาน
        _handler = new CommandHandler(_client, _music);
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