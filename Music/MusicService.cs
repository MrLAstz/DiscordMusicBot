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
        // ถ้ายังไม่ได้เชื่อมต่อ หรือไม่มี ID ห้อง ให้คืนค่าว่าง
        if (_currentChannelId == null) return new List<object>();

        try
        {
            // 1. ดึงข้อมูล Guild จาก Channel ล่าสุด (Cast เป็น SocketGuild เพื่อใช้ความสามารถของ Socket)
            if (_lastChannel?.Guild is not SocketGuild socketGuild) return new List<object>();

            // 2. ดึงข้อมูล Voice Channel แบบสดๆ โดยใช้ ID (สำคัญมาก: ต้องดึงผ่าน socketGuild.GetVoiceChannel)
            var voiceChannel = socketGuild.GetVoiceChannel(_currentChannelId.Value);

            // ถ้าหาห้องไม่เจอ หรือไม่มีคนอยู่ในห้องเลย
            if (voiceChannel == null || voiceChannel.Users == null) return new List<object>();

            // 3. ดึงรายชื่อเฉพาะคนที่อยู่ในห้องนั้นจริงๆ (voiceChannel.Users จะคืนค่าเฉพาะคนในห้องนั้น)
            return voiceChannel.Users.Select(u => new
            {
                name = u.GlobalName ?? u.Username,
                avatar = u.GetAvatarUrl() ?? u.GetDefaultAvatarUrl(),
                // ตรวจสอบสถานะ: ถ้าใช้ SocketUser จะได้สถานะที่แม่นยำกว่า
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