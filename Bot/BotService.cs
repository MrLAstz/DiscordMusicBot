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
            // 1. ตรวจสอบว่าใช้ GatewayIntents.All หรืออย่างน้อยต้องมี GuildVoiceStates และ GuildMembers
            GatewayIntents = GatewayIntents.All,
            AlwaysDownloadUsers = true // 2. เพิ่มบรรทัดนี้เพื่อให้บอทเก็บข้อมูล User ไว้ใน Cache เสมอ
        });

        // 3. ส่งตัว client นี้ไปให้ MusicService ใช้งาน (ต้องไปสร้าง Method นี้ใน MusicService ด้วย)
        _music.SetDiscordClient(_client);

        new CommandHandler(_client, _music);
    }

    public async Task StartAsync()
    {
        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();

        Console.WriteLine("🤖 Discord Bot started");
    }
}
