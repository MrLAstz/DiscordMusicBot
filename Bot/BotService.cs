using Discord;
using Discord.WebSocket;
using DiscordMusicBot.Music;

namespace DiscordMusicBot.Bot;

public class BotService
{
    private readonly DiscordSocketClient _client;
    private readonly string _token;
    private readonly MusicService _music;
    private readonly CommandHandler _handler;

    public BotService(string token, MusicService music)
    {
        _token = token;
        _music = music;

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds |            // เพื่อให้บอทเห็นเซิร์ฟเวอร์
                             GatewayIntents.GuildMembers |       // เพื่อดึงรายชื่อคน (หน้าเว็บใช้)
                             GatewayIntents.GuildVoiceStates |    // เพื่อให้บอทเข้าห้องเสียงได้
                             GatewayIntents.MessageContent,      // ถ้าคุณต้องการอ่านข้อความ (ถ้าไม่ใช้ลบได้)
            AlwaysDownloadUsers = true
        };

        _client = new DiscordSocketClient(config);
        _music.SetDiscordClient(_client);

        _handler = new CommandHandler(_client, _music);
    }

    public async Task StartAsync()
    {
        _client.Log += LogAsync;
        _client.Ready += RegisterCommandsAsync;

        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();
    }
    // ตรวจสอบว่ามีก้อนนี้อยู่ในคลาส BotService
    private Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }
}

