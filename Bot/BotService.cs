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
            GatewayIntents = GatewayIntents.AllUnprivileged |
                             GatewayIntents.GuildMembers |
                             GatewayIntents.GuildPresences |
                             GatewayIntents.MessageContent |
                             GatewayIntents.GuildVoiceStates,
            AlwaysDownloadUsers = true
        };

        _client = new DiscordSocketClient(config);
        _music.SetDiscordClient(_client);

        _handler = new CommandHandler(_client, _music);
    }

    public async Task StartAsync()
    {
        _client.Log += LogAsync;
        _client.Ready += OnReadyAsync;

        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();
    }

    private async Task OnReadyAsync()
    {
        var commands = new List<SlashCommandBuilder>
        {
            new SlashCommandBuilder().WithName("help").WithDescription("ดูเมนูคำสั่งทั้งหมดของบอท"),
            new SlashCommandBuilder().WithName("join").WithDescription("ให้บอทเข้าห้องเสียงที่คุณอยู่"),
            new SlashCommandBuilder().WithName("status").WithDescription("เช็คสถานะการเชื่อมต่อและสมาชิกในห้อง"),
            new SlashCommandBuilder().WithName("play").WithDescription("เล่นเพลงจาก YouTube")
                .AddOption("url", ApplicationCommandOptionType.String, "วางลิงก์ YouTube ที่นี่", isRequired: true)
        };

        try
        {
            foreach (var cmd in commands)
            {
                await _client.CreateGlobalApplicationCommandAsync(cmd.Build());
            }
            Console.WriteLine("✅ ลงทะเบียนเมนู Slash Commands สำเร็จ!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ลงทะเบียนเมนูพลาด: {ex.Message}");
        }
    }

    private Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }
}