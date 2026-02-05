using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Collections.Concurrent;

namespace DiscordMusicBot.Music;

public class MusicService
{
    private readonly ConcurrentDictionary<ulong, IAudioClient> _audioClients = new();
    // เก็บตัวจัดการการยกเลิกงาน (เพื่อหยุดหรือข้ามเพลง)
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
                // หยุดเพลงเดิมก่อนถ้ามีเล่นอยู่
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
                    // เล่นเพลงในพื้นหลังเพื่อให้หน้าเว็บทำงานต่อได้ทันที
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var stream = await _youtube.GetAudioStreamAsync(url);
                            using var discord = audioClient.CreatePCMStream(AudioApplication.Music);

                            // ส่งเสียงพร้อม Token เพื่อให้สามารถยกเลิก (Skip) ได้
                            await stream.CopyToAsync(discord, newCts.Token);
                            await discord.FlushAsync();
                        }
                        catch (OperationCanceledException) { /* เพลงถูกข้ามโดยเจตนา */ }
                        catch (Exception ex) { Console.WriteLine($"Playback Error: {ex.Message}"); }
                        finally
                        {
                            _cts.TryRemove(guild.Id, out _);
                        }
                    }, newCts.Token);
                }
                return;
            }
        }
    }

    // ✅ เพิ่มฟังก์ชัน Toggle (Pause/Resume) 
    // สำหรับ Discord.Net วิธีที่เสถียรที่สุดคือการหยุด Stream ปัจจุบัน
    public async Task ToggleAsync(ulong userId)
    {
        await SkipAsync(userId);
    }

    // ✅ เพิ่มฟังก์ชัน Skip (หยุดเพลงที่กำลังเล่นอยู่)
    public async Task SkipAsync(ulong userId)
    {
        if (_discordClient == null) return;

        foreach (var guild in _discordClient.Guilds)
        {
            var user = guild.GetUser(userId);
            if (user?.VoiceChannel != null)
            {
                if (_cts.TryRemove(guild.Id, out var oldCts))
                {
                    oldCts.Cancel();
                    oldCts.Dispose();
                    Console.WriteLine($"⏭️ Skipped track in: {guild.Name}");
                }
                return;
            }
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
                    var restUser = await _discordClient.Rest.GetGuildUserAsync(g.Id, userId);
                    if (restUser != null) user = g.GetUser(restUser.Id);
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