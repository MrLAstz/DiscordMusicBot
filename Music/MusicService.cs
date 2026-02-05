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
    private IVoiceChannel? _lastChannel; // จะถูก update ตามตำแหน่งปัจจุบันของ User เสมอ

    public string CurrentGuildName { get; set; } = "ไม่ได้เชื่อมต่อ";

    public void SetDiscordClient(DiscordSocketClient client)
    {
        _discordClient = client;
    }

    public async Task JoinAsync(IVoiceChannel channel)
    {
        // Stateless: เราจะไม่สนห้องเก่า แต่จะกระโดดเข้าห้องปัจจุบันที่ส่งมาทันที
        _lastChannel = channel;
        _serverRooms[channel.Guild.Id] = channel.Id;
        CurrentGuildName = channel.Guild.Name;

        // เชื่อมต่อใหม่เสมอเพื่อให้แน่ใจว่าบอทอยู่ในห้องเดียวกับ User
        var audioClient = await channel.ConnectAsync();
        _audioClients[channel.Guild.Id] = audioClient;
    }

    public async Task<bool> JoinByUserIdAsync(ulong userId)
    {
        if (_discordClient == null) return false;

        // Stateless Check: วนหาตำแหน่งจริงของ User ณ วินาทีนี้ในทุก Guild
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
        // ระบบ Fallback: ถ้าหา User ไม่เจอจริงๆ ถึงจะสุ่มเข้าห้องที่มีคน
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
            var user = guild.GetUser(userId);
            // Stateless Play: ต้องเจอตัว User ในห้อง Voice ก่อนถึงจะเล่น
            if (user?.VoiceChannel != null)
            {
                // ถ้าบอทไม่ได้อยู่ในห้องเดียวกับ User ให้สั่ง Join ก่อน
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
        // หากไม่มี userId ส่งมา ให้พยายามเล่นในห้องที่บอทแฝงตัวอยู่ปัจจุบัน
        if (_lastChannel != null && _audioClients.TryGetValue(_lastChannel.Guild.Id, out var audioClient))
        {
            var stream = await _youtube.GetAudioStreamAsync(url);
            using var discord = audioClient.CreatePCMStream(AudioApplication.Music);
            await stream.CopyToAsync(discord);
            await discord.FlushAsync();
        }
    }

    public object GetUsersInVoice(ulong userId)
    {
        if (_discordClient == null || _discordClient.Guilds.Count == 0)
            return new { guild = "บอทยังไม่พร้อม", users = new List<object>() };

        try
        {
            // วนหาว่าผู้ใช้ที่ล็อกอิน (userId) อยู่ใน Guild ไหนและห้องไหน ณ ตอนนี้
            foreach (var guild in _discordClient.Guilds)
            {
                var user = guild.GetUser(userId);

                // ตรวจสอบว่าผู้ใช้คนนี้อยู่ใน Voice Channel หรือไม่
                if (user?.VoiceChannel != null)
                {
                    var targetChannel = user.VoiceChannel;
                    this.CurrentGuildName = guild.Name;
                    _lastChannel = targetChannel; // อัปเดตห้องล่าสุดที่ตรวจพบ

                    // ดึงข้อมูลสมาชิกเฉพาะที่อยู่ใน Voice Channel เดียวกันเท่านั้น
                    var userList = targetChannel.Users.Select(u => new
                    {
                        name = u.GlobalName ?? u.Username,
                        avatar = u.GetAvatarUrl() ?? u.GetDefaultAvatarUrl(),
                        status = u.Status.ToString().ToLower()
                    }).ToList();

                    return new { guild = guild.Name, users = userList };
                }
            }

            // กรณีล็อกอินแล้วแต่ไม่ได้เข้าห้องเสียงใดๆ
            return new { guild = "ไม่ได้อยู่ในห้องเสียง", users = new List<object>() };
        }
        catch
        {
            return new { guild = "Error", users = new List<object>() };
        }
    }
}