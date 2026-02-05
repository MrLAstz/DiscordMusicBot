using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Diagnostics;

namespace DiscordMusicBot.Music;

public class MusicService
{
    private IVoiceChannel? _lastChannel;
    private IAudioClient? _client;
    private readonly YoutubeService _youtube = new();

    // 1. เพิ่มตัวแปรเก็บชื่อ Server ไว้ด้านบน
    public string CurrentGuildName { get; set; } = "ไม่ได้เชื่อมต่อ";

    // 2. ใช้ JoinAsync ตัวนี้ตัวเดียวพอ (รวมร่างแล้ว)
    public async Task JoinAsync(IVoiceChannel channel)
    {
        _lastChannel = channel;
        // เก็บชื่อ Server เมื่อเชื่อมต่อ
        CurrentGuildName = channel.Guild.Name;

        _client ??= await channel.ConnectAsync();
    }

    public async Task JoinLastAsync()
    {
        if (_lastChannel != null && _client == null)
        {
            // อัปเดตชื่อ Guild เมื่อกลับมาเชื่อมต่อห้องเดิมด้วย
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

    public List<string> GetUsersInVoice()
    {
        if (_lastChannel == null) return new List<string>();

        // ดึงชื่อของทุกคนในห้อง (รวมถึงบอทด้วย)
        return _lastChannel.GetUsersAsync().FlattenAsync().Result
            .Select(u => u.GlobalName ?? u.Username)
            .ToList();
    }
}