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

        var audioClient = await channel.ConnectAsync();
        _audioClients[channel.Guild.Id] = audioClient;
    }

    public async Task<bool> JoinByUserIdAsync(ulong userId)
    {
        if (_discordClient == null) return false;

        foreach (var guild in _discordClient.Guilds)
        {
            var user = guild.GetUser(userId) ?? await guild.Rest.GetGuildUserAsync(userId);
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
        if (_discordClient != null)
        {
            var firstGuild = _discordClient.Guilds.FirstOrDefault();
            if (firstGuild != null)
            {
                var target = firstGuild.VoiceChannels
                    .OrderByDescending(v => v.Users.Count)
                    .FirstOrDefault();

                if (target != null) await JoinAsync(target);
            }
        }
    }

    public async Task PlayByUserIdAsync(ulong userId, string url)
    {
        if (_discordClient == null) return;

        foreach (var guild in _discordClient.Guilds)
        {
            var user = guild.GetUser(userId) ?? await guild.Rest.GetGuildUserAsync(userId);
            if (user?.VoiceChannel != null)
            {
                if (!_audioClients.ContainsKey(guild.Id))
                {
                    await JoinAsync(user.VoiceChannel);
                }

                var stream = await _youtube.GetAudioStreamAsync(url);
                if (_audioClients.TryGetValue(guild.Id, out var audioClient))
                {
                    using var discord = audioClient.CreatePCMStream(AudioApplication.Music);
                    await stream.CopyToAsync(discord);
                    await discord.FlushAsync();
                    return;
                }
            }
        }
    }

    public async Task PlayLastAsync(string url)
    {
        if (_lastChannel != null && _audioClients.TryGetValue(_lastChannel.Guild.Id, out var audioClient))
        {
            var stream = await _youtube.GetAudioStreamAsync(url);
            using var discord = audioClient.CreatePCMStream(AudioApplication.Music);
            await stream.CopyToAsync(discord);
            await discord.FlushAsync();
        }
    }

    public async Task<object> GetUsersInVoice(ulong userId)
    {
        if (_discordClient == null || _discordClient.ConnectionState != ConnectionState.Connected)
            return new { guild = "กำลังเชื่อมต่อ Discord...", users = new List<object>() };

        SocketGuildUser? user = null;
        SocketGuild? targetGuild = null;

        foreach (var g in _discordClient.Guilds)
        {
            user = g.GetUser(userId);
            if (user == null)
            {
                try
                {
                    var restUser = await g.Rest.GetGuildUserAsync(userId);
                    user = g.GetUser(restUser.Id);
                }
                catch { continue; }
            }

            if (user != null)
            {
                targetGuild = g;
                break;
            }
        }

        if (user == null || targetGuild == null)
            return new { guild = "ไม่พบข้อมูลสมาชิกในระบบ", users = new List<object>() };

        if (user.VoiceChannel == null)
            return new { guild = "คุณไม่ได้อยู่ในห้องเสียง", users = new List<object>() };

        var channel = user.VoiceChannel;

        var usersInRoom = targetGuild.Users
            .Where(u => u.VoiceChannel?.Id == channel.Id)
            .Select(u => new
            {
                name = u.GlobalName ?? u.Username,
                avatar = u.GetAvatarUrl() ?? u.GetDefaultAvatarUrl(),
                status = u.Status.ToString().ToLower()
            })
            .ToList();

        return new
        {
            guild = $"{targetGuild.Name} ({channel.Name})",
            users = usersInRoom
        };
    }
}