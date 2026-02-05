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

    // ✅ เพิ่มฟังก์ชันใหม่: ให้บอทวิ่งไปหา userId ที่ระบุ
    public async Task<bool> JoinByUserIdAsync(ulong userId)
    {
        if (_discordClient == null) return false;

        foreach (var guild in _discordClient.Guilds)
        {
            var user = guild.GetUser(userId);
            if (user?.VoiceChannel != null)
            {
                await JoinAsync(user.VoiceChannel);
                return true;
            }
        }
        return false;
    }

    public async Task JoinLastAsync()
    {
        if (_lastChannel == null && _discordClient != null)
        {
            var firstGuild = _discordClient.Guilds.FirstOrDefault();
            if (firstGuild != null)
            {
                _lastChannel = firstGuild.VoiceChannels
                    .OrderByDescending(v => v.Users.Count)
                    .FirstOrDefault();
            }
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

    public object GetUsersInVoice(ulong? guildId = null)
    {
        if (_discordClient == null || _discordClient.Guilds.Count == 0)
            return new { guild = "บอทยังไม่พร้อม", users = new List<object>() };

        try
        {
            SocketGuild? guild = null;

            if (guildId.HasValue)
            {
                guild = _discordClient.GetGuild(guildId.Value);
            }

            if (guild == null)
            {
                guild = _lastChannel?.Guild as SocketGuild ?? _discordClient.Guilds.FirstOrDefault();
            }

            if (guild == null) return new { guild = "ไม่พบเซิร์ฟเวอร์", users = new List<object>() };

            this.CurrentGuildName = guild.Name;

            _serverRooms.TryGetValue(guild.Id, out var joinedChannelId);

            var targetChannel = guild.VoiceChannels
                                .FirstOrDefault(c => c.Id == joinedChannelId)
                                ?? guild.VoiceChannels
                                    .OrderByDescending(v => v.Users.Count)
                                    .FirstOrDefault();

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