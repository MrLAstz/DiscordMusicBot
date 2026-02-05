using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Collections.Concurrent;

namespace DiscordMusicBot.Music;

public class MusicService
{
    private readonly ConcurrentDictionary<ulong, IAudioClient> _audioClients = new();
    // 1. เพิ่มตัวจัดการยกเลิกงาน (เพื่อหยุดเพลงเก่าตอนกดเพลงใหม่)
    private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _cts = new();
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
        CurrentGuildName = channel.Guild.Name;

        var audioClient = await channel.ConnectAsync();
        _audioClients[channel.Guild.Id] = audioClient;
    }

    public async Task<bool> JoinByUserIdAsync(ulong userId)
    {
        if (_discordClient == null) return false;

        foreach (var guild in _discordClient.Guilds)
        {
            var user = guild.GetUser(userId) ?? (await _discordClient.Rest.GetGuildUserAsync(guild.Id, userId) as IGuildUser);
            if (user?.VoiceChannel != null)
            {
                await JoinAsync(user.VoiceChannel);
                return true;
            }
        }
        return false;
    }

    public async Task PlayByUserIdAsync(ulong userId, string url)
    {
        if (_discordClient == null) return;

        foreach (var guild in _discordClient.Guilds)
        {
            var user = guild.GetUser(userId) ?? (await _discordClient.Rest.GetGuildUserAsync(guild.Id, userId) as IGuildUser);
            if (user?.VoiceChannel != null)
            {
                // 2. ถ้ามีเพลงเดิมเล่นอยู่ ให้สั่งหยุดก่อน (เหมือน Spotify เปลี่ยนเพลง)
                if (_cts.TryRemove(guild.Id, out var oldCts))
                {
                    oldCts.Cancel();
                    oldCts.Dispose();
                }

                var newCts = new CancellationTokenSource();
                _cts[guild.Id] = newCts;

                if (!_audioClients.ContainsKey(guild.Id))
                {
                    await JoinAsync(user.VoiceChannel);
                }

                if (_audioClients.TryGetValue(guild.Id, out var audioClient))
                {
                    // 3. รันการเล่นเพลงใน Task.Run เพื่อให้หน้าเว็บไม่ค้างขณะรอโหลดเพลง
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var stream = await _youtube.GetAudioStreamAsync(url);
                            using var discord = audioClient.CreatePCMStream(AudioApplication.Music);

                            // ส่ง Token เข้าไป เพื่อให้ยกเลิกการส่งเสียงได้ทันที
                            await stream.CopyToAsync(discord, newCts.Token);
                            await discord.FlushAsync();
                        }
                        catch (OperationCanceledException) { /* เพลงถูกข้าม */ }
                        catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
                    }, newCts.Token);
                }
                return;
            }
        }
    }

    // ส่วนของ GetUsersInVoice คงเดิมตามที่คุณส่งมา...
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
                    var restUser = await _discordClient.Rest.GetGuildUserAsync(g.Id, userId);
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

        return new { guild = $"{targetGuild.Name} ({channel.Name})", users = usersInRoom };
    }
}