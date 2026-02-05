using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Collections.Concurrent;
using System.Linq;

namespace DiscordMusicBot.Music;

public class MusicService
{
    private readonly ConcurrentDictionary<ulong, ulong> _serverRooms = new();
    private readonly ConcurrentDictionary<ulong, IAudioClient> _audioClients = new();
    private DiscordSocketClient? _discordClient;
    private readonly YoutubeService _youtube = new();

    // ✅ เพิ่มตัวแปรนี้เพื่อจำห้องล่าสุดที่บอทเข้าไป
    private IVoiceChannel? _lastChannel;

    public string CurrentGuildName { get; set; } = "ไม่ได้เชื่อมต่อ";

    public void SetDiscordClient(DiscordSocketClient client)
    {
        _discordClient = client;
    }

    // ✅ แก้ไข: เพิ่ม Overload ให้ JoinAsync รับห้องมาเก็บไว้ (ใช้ตอน Command ใน Discord)
    public async Task JoinAsync(IVoiceChannel channel)
    {
        _lastChannel = channel; // จำห้องนี้ไว้
        _serverRooms[channel.Guild.Id] = channel.Id;
        CurrentGuildName = channel.Guild.Name;

        if (!_audioClients.ContainsKey(channel.Guild.Id))
        {
            var audioClient = await channel.ConnectAsync();
            _audioClients[channel.Guild.Id] = audioClient;
        }
    }

    // ✅ แก้ไข: ทำให้ JoinLastAsync ไม่มี parameter (เพื่อให้ WebServer เรียกใช้ได้)
    public async Task JoinLastAsync()
    {
        if (_lastChannel != null)
        {
            await JoinAsync(_lastChannel);
        }
    }

    // ✅ แก้ไข: ทำให้ PlayLastAsync รับแค่ URL (เพื่อให้ WebServer เรียกใช้ได้)
    public async Task PlayLastAsync(string url)
    {
        if (_lastChannel == null) return;

        var stream = await _youtube.GetAudioStreamAsync(url);
        if (_audioClients.TryGetValue(_lastChannel.Guild.Id, out var audioClient))
        {
            using var discord = audioClient.CreatePCMStream(AudioApplication.Music);
            await stream.CopyToAsync(discord);
            await discord.FlushAsync();
        }
    }

    // ✅ แก้ไข: เปลี่ยนชื่อเป็น GetUsersInVoice (เพื่อให้ตรงกับที่ WebServer เรียก)
    public object GetUsersInVoice(ulong? guildId = null)
    {
        if (_discordClient == null)
            return new { guild = "บอทยังไม่พร้อม", users = new List<object>() };

        var targetGuildId = guildId ?? _serverRooms.Keys.FirstOrDefault();

        if (targetGuildId == 0 || !_serverRooms.TryGetValue(targetGuildId, out var channelId))
            return new { guild = "ไม่ได้เชื่อมต่อ", users = new List<object>() };

        try
        {
            var voiceChannel = _discordClient.GetChannel(channelId) as SocketVoiceChannel;
            if (voiceChannel == null)
                return new { guild = "ไม่พบห้อง", users = new List<object>() };

            this.CurrentGuildName = voiceChannel.Guild.Name;

            return new
            {
                guild = voiceChannel.Guild.Name,
                users = voiceChannel.Users
                    .Where(u => u.VoiceChannel != null && u.VoiceChannel.Id == channelId)
                    .Select(u => new
                    {
                        name = u.GlobalName ?? u.Username,
                        avatar = u.GetAvatarUrl() ?? u.GetDefaultAvatarUrl(),
                        status = u.Status.ToString().ToLower()
                    }).ToList()
            };
        }
        catch
        {
            return new { guild = "Error", users = new List<object>() };
        }
    }
}