using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Diagnostics;
using System.Linq; // ✅ ต้องเพิ่มบรรทัดนี้ ไม่งั้นจะ Error ที่ .Select และ .ToList

namespace DiscordMusicBot.Music;

public class MusicService
{
    private IVoiceChannel? _lastChannel;
    private IAudioClient? _client;
    private ulong? _currentChannelId;
    private readonly YoutubeService _youtube = new();

    public string CurrentGuildName { get; set; } = "ไม่ได้เชื่อมต่อ";

    public async Task JoinAsync(IVoiceChannel channel)
    {
        _lastChannel = channel;
        _currentChannelId = channel.Id; // จำ ID ห้องไว้
        CurrentGuildName = channel.Guild.Name;
        _client ??= await channel.ConnectAsync();
    }

    public async Task JoinLastAsync()
    {
        if (_lastChannel != null && _client == null)
        {
            CurrentGuildName = _lastChannel.Guild.Name;
            _client = await _lastChannel.ConnectAsync();
        }
    }

    public async Task PlayAsync(IVoiceChannel channel, string url)
    {
        await JoinAsync(channel);
        await PlayInternal(url);
    }

    public async Task PlayLastAsync(string url)
    {
        if (_client == null) return;
        await PlayInternal(url);
    }

    private async Task PlayInternal(string url)
    {
        var stream = await _youtube.GetAudioStreamAsync(url);
        using var discord = _client!.CreatePCMStream(AudioApplication.Music);
        await stream.CopyToAsync(discord);
        await discord.FlushAsync();
    }

    // ✅ แก้ไขส่วนนี้เพื่อให้ดึงรายชื่อได้ถูกต้อง
    public object GetUsersInVoice()
    {
        if (_lastChannel == null || _currentChannelId == null) return new List<object>();

        try
        {
            // 1. ดึงข้อมูล Guild ใหม่จาก Socket เพื่อให้ได้ข้อมูลล่าสุด
            var guild = _lastChannel.Guild as SocketGuild;
            // 2. ดึง Channel โดยระบุ ID
            var voiceChannel = guild?.GetVoiceChannel(_currentChannelId.Value);

            if (voiceChannel == null) return new List<object>();

            // 3. ใช้ .Users จาก SocketVoiceChannel (ซึ่งจะถูก Update โดย Gateway Intents)
            var currentUsers = voiceChannel.Users;

            return currentUsers.Select(u => new
            {
                name = u.GlobalName ?? u.Username,
                avatar = u.GetAvatarUrl() ?? u.GetDefaultAvatarUrl(),
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