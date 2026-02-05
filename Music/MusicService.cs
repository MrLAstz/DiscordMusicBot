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
    private readonly YoutubeService _youtube = new();

    public string CurrentGuildName { get; set; } = "ไม่ได้เชื่อมต่อ";

    public async Task JoinAsync(IVoiceChannel channel)
    {
        _lastChannel = channel;
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
    public List<string> GetUsersInVoice()
    {
        if (_lastChannel == null) return new List<string>();

        try
        {
            // ดึงรายชื่อ User ทั้งหมดใน Channel (ต้องรันแบบ Sync ในที่นี้ใช้ .Result)
            var users = _lastChannel.GetUsersAsync().FlattenAsync().Result;

            return users
                .Select(u => u.GlobalName ?? u.Username) // เลือกชื่อเล่น (GlobalName) ถ้าไม่มีใช้ Username
                .Where(name => !string.IsNullOrEmpty(name)) // กรองชื่อที่ว่างออก
                .ToList();
        }
        catch
        {
            return new List<string> { "ไม่สามารถดึงข้อมูลได้" };
        }
    }
}