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
    private IVoiceChannel? _lastChannel;

    public string CurrentGuildName { get; set; } = "ไม่ได้เชื่อมต่อ";

    public void SetDiscordClient(DiscordSocketClient client)
    {
        _discordClient = client;
    }

    public async Task JoinAsync(IVoiceChannel channel)
    {
        _lastChannel = channel;
        _serverRooms[channel.Guild.Id] = channel.Id;
        CurrentGuildName = channel.Guild.Name;

        if (!_audioClients.ContainsKey(channel.Guild.Id))
        {
            var audioClient = await channel.ConnectAsync();
            _audioClients[channel.Guild.Id] = audioClient;
        }
    }

    public async Task JoinLastAsync()
    {
        // ถ้าไม่มี _lastChannel ให้พยายามหาห้องที่มีคนนั่งอยู่จาก Guild แรกที่บอทอยู่
        if (_lastChannel == null && _discordClient != null)
        {
            var firstGuild = _discordClient.Guilds.FirstOrDefault();
            _lastChannel = firstGuild?.VoiceChannels
                .OrderByDescending(v => v.Users.Count)
                .FirstOrDefault();
        }

        if (_lastChannel != null)
        {
            await JoinAsync(_lastChannel);
        }
    }

    public async Task PlayLastAsync(string url)
    {
        if (_lastChannel == null) await JoinLastAsync();
        if (_lastChannel == null) return;

        var stream = await _youtube.GetAudioStreamAsync(url);
        if (_audioClients.TryGetValue(_lastChannel.Guild.Id, out var audioClient))
        {
            using var discord = audioClient.CreatePCMStream(AudioApplication.Music);
            await stream.CopyToAsync(discord);
            await discord.FlushAsync();
        }
    }

    // ✅ Logic ใหม่: ดึงข้อมูลสดจาก Discord ทันที
    public object GetUsersInVoice(ulong? guildId = null)
    {
        if (_discordClient == null || _discordClient.Guilds.Count == 0)
            return new { guild = "บอทยังไม่พร้อม", users = new List<object>() };

        try
        {
            // 1. เลือก Guild ที่จะแสดง: ตาม ID ที่ส่งมา หรือ Guild ล่าสุด หรือ Guild แรกที่เจอ
            var guild = (guildId.HasValue ? _discordClient.GetGuild(guildId.Value) : null)
                        ?? _lastChannel?.Guild
                        ?? _discordClient.Guilds.First();

            this.CurrentGuildName = guild.Name;

            // 2. เลือกห้องที่จะแสดงสมาชิก: ห้องที่บอทอยู่ หรือถ้าไม่มี ให้เอาห้องที่มีคนเยอะที่สุด
            _serverRooms.TryGetValue(guild.Id, out var joinedChannelId);

            var targetChannel = guild.VoiceChannels.FirstOrDefault(c => c.Id == joinedChannelId)
                                ?? guild.VoiceChannels.OrderByDescending(v => v.Users.Count).FirstOrDefault();

            if (targetChannel == null)
                return new { guild = guild.Name, users = new List<object>() };

            var userList = targetChannel.Users.Select(u => new
            {
                name = u.GlobalName ?? u.Username,
                avatar = u.GetAvatarUrl() ?? u.GetDefaultAvatarUrl(),
                status = u.Status.ToString().ToLower()
            }).ToList();

            return new { guild = guild.Name, users = userList };
        }
        catch
        {
            return new { guild = "Error", users = new List<object>() };
        }
    }
}