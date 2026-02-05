using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Diagnostics;
using System.Linq;

namespace DiscordMusicBot.Music;

public class MusicService
{
    private IVoiceChannel? _lastChannel;
    private IAudioClient? _audioClient; // เปลี่ยนชื่อเพื่อไม่ให้สับสนกับ SocketClient
    private DiscordSocketClient? _discordClient; // สำหรับดึงข้อมูล User สดๆ
    private ulong? _currentChannelId;
    private readonly YoutubeService _youtube = new();

    public string CurrentGuildName { get; set; } = "ไม่ได้เชื่อมต่อ";

    // ✅ Method สำหรับรับ Client มาจาก BotService
    public void SetDiscordClient(DiscordSocketClient client)
    {
        _discordClient = client;
    }

    public async Task JoinAsync(IVoiceChannel channel)
    {
        _lastChannel = channel;
        _currentChannelId = channel.Id;
        CurrentGuildName = channel.Guild.Name;
        _audioClient ??= await channel.ConnectAsync();
    }

    public async Task JoinLastAsync()
    {
        if (_lastChannel != null && _audioClient == null)
        {
            CurrentGuildName = _lastChannel.Guild.Name;
            _audioClient = await _lastChannel.ConnectAsync();
        }
    }

    public async Task PlayAsync(IVoiceChannel channel, string url)
    {
        await JoinAsync(channel);
        await PlayInternal(url);
    }

    public async Task PlayLastAsync(string url)
    {
        if (_audioClient == null) return;
        await PlayInternal(url);
    }

    private async Task PlayInternal(string url)
    {
        var stream = await _youtube.GetAudioStreamAsync(url);
        using var discord = _audioClient!.CreatePCMStream(AudioApplication.Music);
        await stream.CopyToAsync(discord);
        await discord.FlushAsync();
    }

    // ✅ แก้ไข: ดึงรายชื่อผ่าน DiscordSocketClient เพื่อความแม่นยำ
    public object GetUsersInVoice()
    {
        if (_discordClient == null || _currentChannelId == null) return new List<object>();

        try
        {
            // ดึง Channel แบบสดๆ จาก Client หลัก (จะไม่อ้างอิง Object เก่าใน Cache)
            var voiceChannel = _discordClient.GetChannel(_currentChannelId.Value) as SocketVoiceChannel;

            if (voiceChannel == null) return new List<object>();

            // ดึงรายชื่อเฉพาะคนที่อยู่ในห้องนั้นจริงๆ
            return voiceChannel.Users.Select(u => new
            {
                name = u.GlobalName ?? u.Username,
                avatar = u.GetAvatarUrl() ?? u.GetDefaultAvatarUrl(),
                // แสดงสถานะ Online/Idle/Dnd
                status = u.Status.ToString().ToLower()
            }).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] GetUsersInVoice: {ex.Message}");
            return new List<object>();
        }
    }
}