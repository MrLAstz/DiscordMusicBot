using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DiscordMusicBot.Music;

public class MusicService
{
    private readonly ConcurrentDictionary<ulong, IAudioClient> _audioClients = new();
    private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _cts = new();
    private DiscordSocketClient? _discordClient;
    private readonly YoutubeService _youtube = new();

    public void SetDiscordClient(DiscordSocketClient client) => _discordClient = client;

    public async Task JoinAsync(IVoiceChannel channel)
    {
        var audioClient = await channel.ConnectAsync();
        _audioClients[channel.Guild.Id] = audioClient;
    }

    public async Task PlayByUserIdAsync(ulong userId, string url)
    {
        if (_discordClient == null) return;

        foreach (var guild in _discordClient.Guilds)
        {
            var user = guild.GetUser(userId) ?? (await _discordClient.Rest.GetGuildUserAsync(guild.Id, userId) as IGuildUser);

            if (user?.VoiceChannel != null)
            {
                // 1. หยุดเพลงเดิม (ถ้ามี)
                if (_cts.TryRemove(guild.Id, out var oldCts))
                {
                    oldCts.Cancel();
                    oldCts.Dispose();
                }

                var newCts = new CancellationTokenSource();
                _cts[guild.Id] = newCts;

                // 2. เช็คการเชื่อมต่อห้องเสียง
                if (!_audioClients.ContainsKey(guild.Id))
                {
                    await JoinAsync(user.VoiceChannel);
                }

                if (_audioClients.TryGetValue(guild.Id, out var audioClient))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // 3. ดึง Direct URL จาก API (เครื่องเราไม่โหลดไฟล์เอง)
                            string streamUrl = await _youtube.GetAudioOnlyUrlAsync(url);

                            // 4. สั่ง FFmpeg ให้ไปดึงข้อมูลจาก YouTube URL นั้น
                            // หมายเหตุ: หากรันบน Windows แล้วบอทไม่เล่น ให้เปลี่ยน "ffmpeg" เป็น Path เต็ม เช่น @"C:\ffmpeg\bin\ffmpeg.exe"
                            var psi = new ProcessStartInfo
                            {
                                FileName = "ffmpeg",
                                Arguments = $"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 -i \"{streamUrl}\" -ac 2 -f s16le -ar 48000 -loglevel panic pipe:1",
                                RedirectStandardOutput = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            using var process = Process.Start(psi);
                            if (process == null) throw new Exception("❌ ไม่สามารถเริ่มต้นโปรแกรม FFmpeg ได้");

                            using var discordStream = audioClient.CreatePCMStream(AudioApplication.Music);

                            // 5. ส่งข้อมูลเสียงเข้าห้อง Discord
                            await process.StandardOutput.BaseStream.CopyToAsync(discordStream, newCts.Token);
                            await discordStream.FlushAsync();
                        }
                        catch (OperationCanceledException) { /* เพลงถูกข้าม */ }
                        catch (Exception ex) { Console.WriteLine($"[Playback Error]: {ex.Message}"); }
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

    public async Task SkipAsync(ulong userId)
    {
        if (_discordClient == null) return;
        foreach (var guild in _discordClient.Guilds)
        {
            if (guild.GetUser(userId)?.VoiceChannel != null)
            {
                if (_cts.TryRemove(guild.Id, out var oldCts))
                {
                    oldCts.Cancel();
                    oldCts.Dispose();
                }
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
            if (user != null) { targetGuild = g; break; }
        }

        if (user == null || targetGuild == null || user.VoiceChannel == null)
            return new { guild = "ไม่พบคุณในห้องเสียง", users = new List<object>() };

        var channel = user.VoiceChannel;
        var usersInRoom = targetGuild.Users
            .Where(u => u.VoiceChannel?.Id == channel.Id)
            .Select(u => new
            {
                name = u.GlobalName ?? u.Username,
                avatar = u.GetAvatarUrl() ?? u.GetDefaultAvatarUrl(),
                status = u.Status.ToString().ToLower()
            }).ToList();

        return new { guild = $"{targetGuild.Name} ({channel.Name})", users = usersInRoom };
    }
}