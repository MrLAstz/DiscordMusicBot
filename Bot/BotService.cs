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

    private readonly TaskCompletionSource<bool> _readyTcs = new();
    public Task ReadyTask => _readyTcs.Task;

    public BotService(string token, MusicService music)
    {
        _token = token;
        _music = music;

        var config = new DiscordSocketConfig
        {
            GatewayIntents =
                GatewayIntents.Guilds |
                GatewayIntents.GuildMembers |
                GatewayIntents.GuildPresences |
                GatewayIntents.GuildVoiceStates |
                GatewayIntents.MessageContent,

            AlwaysDownloadUsers = true,
            LogGatewayIntentWarnings = false
        };

        _client = new DiscordSocketClient(config);

        // 🔥 ผูก MusicService
        _music.SetDiscordClient(_client);
        _music.SetReadyTask(_readyTcs.Task);

        _handler = new CommandHandler(_client, _music);
    }

    public async Task StartAsync()
    {
        _client.Log += LogAsync;
        _client.Ready += OnReadyAsync;

        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();
    }

    public Task WaitUntilReadyAsync()
        => _readyTcs.Task;

    private async Task OnReadyAsync()
    {
        // กัน Ready ยิงซ้ำ (Discord.Net ชอบยิงมากกว่า 1 ครั้ง)
        if (_readyTcs.Task.IsCompleted)
            return;

        Console.WriteLine("🤖 Discord READY");

        var commands = new List<SlashCommandBuilder>
        {
            new SlashCommandBuilder()
                .WithName("help")
                .WithDescription("ดูเมนูคำสั่งทั้งหมดของบอท"),

            new SlashCommandBuilder()
                .WithName("join")
                .WithDescription("ให้บอทเข้าห้องเสียงที่คุณอยู่"),

            new SlashCommandBuilder()
                .WithName("status")
                .WithDescription("เช็คสถานะการเชื่อมต่อและสมาชิกในห้อง"),

            new SlashCommandBuilder()
                .WithName("play")
                .WithDescription("เล่นเพลงจาก YouTube")
                .AddOption(
                    name: "url",
                    type: ApplicationCommandOptionType.String,
                    description: "วางลิงก์ YouTube ที่นี่",
                    isRequired: true
                )
        };

        try
        {
            foreach (var cmd in commands)
                await _client.CreateGlobalApplicationCommandAsync(cmd.Build());

            Console.WriteLine("✅ Slash Commands registered");
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Slash command error");
            Console.WriteLine(ex);
        }

        // 🔓 ปลด READY ให้ MusicService ใช้งานได้
        _readyTcs.TrySetResult(true);
    }

    private Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }
}
