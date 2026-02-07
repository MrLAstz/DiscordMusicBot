using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DiscordMusicBot.Music.Models;
namespace DiscordMusicBot.Music;

/// <summary>
/// 🎵 หัวใจของบอทเพลง
/// - Join voice
/// - Pipe เสียงจาก ffmpeg → Discord
/// - Skip / Toggle / List คนในห้อง
/// </summary>
public class MusicService
{
    // 🎧 guildId → audio client
    private readonly ConcurrentDictionary<ulong, IAudioClient> _audioClients = new();

    // ⏹ guildId → cancellation token (ใช้ stop / skip)
    private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _cts = new();

    // 🚦 กัน join voice ซ้อน
    private readonly SemaphoreSlim _joinLock = new(1, 1);

    // 🤖 Discord client จาก Program.cs
    private DiscordSocketClient? _discordClient;

    // ▶️ YouTube backend
    private readonly YoutubeService _youtube = new();

    // ⏳ รอ Discord Ready
    private Task? _readyTask;

    private readonly ConcurrentDictionary<ulong, MusicQueue> _queues = new();

    private MusicQueue GetQueue(ulong guildId)
        => _queues.GetOrAdd(guildId, _ => new MusicQueue());
    // ===== FIX libopus (Linux / Docker) =====
    // 🐧 ถ้าไม่ทำอันนี้ = เข้า voice ได้ แต่ "เงียบ"
    static MusicService()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            NativeLibrary.SetDllImportResolver(
                typeof(MusicService).Assembly,
                (libraryName, _, _) =>
                {
                    if (libraryName == "opus" || libraryName == "libopus")
                    {
                        foreach (var p in new[] { "libopus.so.0", "libopus.so", "opus.so" })
                            if (NativeLibrary.TryLoad(p, out var h)) return h;
                    }
                    return IntPtr.Zero;
                });
        }
    }


    // 🔌 Inject จาก Program.cs
    public void SetReadyTask(Task readyTask) => _readyTask = readyTask;
    public void SetDiscordClient(DiscordSocketClient client) => _discordClient = client;

    public async Task EnqueueAsync(
    ulong userId,
    string input,
    string requestedBy
)
    {
        if (_discordClient == null) return;

        foreach (var g in _discordClient.Guilds)
        {
            var user = g.GetUser(userId);
            if (user?.VoiceChannel == null) continue;

            var videoId = await _youtube.ResolveVideoIdAsync(input);

            var track = new MusicTrack
            {
                VideoId = videoId,
                Title = videoId,
                RequestedBy = requestedBy
            };

            GetQueue(g.Id).Enqueue(track);

            // ถ้ายังไม่เล่น → เริ่ม loop
            if (!_cts.ContainsKey(g.Id))
                _ = PlayQueueAsync(g, user.VoiceChannel);

            return;
        }
    }

    private async Task PlayQueueAsync(
    SocketGuild guild,
    IVoiceChannel channel
)
    {
        var queue = GetQueue(guild.Id);

        var cts = new CancellationTokenSource();
        _cts[guild.Id] = cts;

        var audio = await JoinAsync(channel);
        if (audio == null) return;

        try
        {
            while (!cts.IsCancellationRequested &&
                   queue.TryDequeue(out var track))
            {
                var audioUrl =
                    await _youtube.GetAudioOnlyUrlAsync(track.VideoId);

                await PlayFfmpegAsync(audio, audioUrl, cts.Token);
            }
        }
        finally
        {
            _cts.TryRemove(guild.Id, out _);
        }
    }


    // ===== JOIN BY USER ID =====
    // 👤 หา user อยู่ guild ไหน → join ห้องนั้น
    public async Task<bool> JoinByUserIdAsync(ulong userId)
    {
        if (_readyTask != null)
            await _readyTask;

        if (_discordClient == null)
            return false;

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

    // ===== JOIN VOICE =====
    // 🔊 เข้าห้อง voice แบบ safe + ไม่ซ้อน
    public async Task<IAudioClient?> JoinAsync(IVoiceChannel channel)
    {
        if (_readyTask != null)
            await _readyTask;

        await _joinLock.WaitAsync();
        try
        {
            // ✅ ถ้าเชื่อมอยู่แล้ว ไม่ต้อง join ซ้ำ
            if (_audioClients.TryGetValue(channel.Guild.Id, out var existing) &&
                existing.ConnectionState == ConnectionState.Connected)
            {
                return existing;
            }

            // 🔁 เคลียร์ client เก่า (กันหลุด 4006)
            if (_audioClients.TryRemove(channel.Guild.Id, out IAudioClient old))
            {
                try
                {
                    await old.StopAsync();
                    old.Dispose();
                }
                catch { }
            }

            Console.WriteLine("🔊 Connecting voice...");

            var client = await channel.ConnectAsync(selfMute: false, selfDeaf: false);

            // ⏱ รอจนกว่าจะ connected จริง
            var timeout = Stopwatch.StartNew();
            while (client.ConnectionState != ConnectionState.Connected)
            {
                if (timeout.ElapsedMilliseconds > 10_000)
                {
                    Console.WriteLine("❌ Voice connect timeout");
                    await client.StopAsync();
                    return null;
                }

                await Task.Delay(200);
            }

            // 🔌 ถ้าหลุด → ล้าง state
            client.Disconnected += _ =>
            {
                Console.WriteLine("🔌 Voice disconnected");
                _audioClients.TryRemove(channel.Guild.Id, out IAudioClient old);
                return Task.CompletedTask;
            };

            _audioClients[channel.Guild.Id] = client;
            Console.WriteLine("✅ Voice connected");

            return client;
        }
        finally
        {
            _joinLock.Release();
        }
    }

    private async Task PlayFfmpegAsync(
    IAudioClient audio,
    string audioUrl,
    CancellationToken token
)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments =
                "-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 " +
                "-headers \"User-Agent: Mozilla/5.0\r\n\" " +
                $"-i \"{audioUrl}\" " +
                "-vn -ac 2 -ar 48000 -f s16le pipe:1",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var ffmpeg = Process.Start(psi);
        if (ffmpeg == null) return;

        _ = Task.Run(async () =>
        {
            while (!ffmpeg.StandardError.EndOfStream)
            {
                var line = await ffmpeg.StandardError.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(line))
                    Console.WriteLine("[ffmpeg] " + line);
            }
        });

        using var discord = audio.CreatePCMStream(AudioApplication.Music);
        await audio.SetSpeakingAsync(true);

        await ffmpeg.StandardOutput.BaseStream.CopyToAsync(
            discord, 32768, token
        );

        await discord.FlushAsync();
        await audio.SetSpeakingAsync(false);
    }


    // ===== PLAY =====
    // ▶️ เล่นเพลงจาก keyword หรือ YouTube URL
    public async Task PlayByUserIdAsync(ulong userId, string input)
    {
        if (_discordClient == null) return;

        foreach (var guild in _discordClient.Guilds)
        {
            var user = guild.GetUser(userId);
            if (user?.VoiceChannel == null) continue;

            // ⏹ stop เพลงเก่า
            if (_cts.TryRemove(guild.Id, out var old))
            {
                old.Cancel();
                old.Dispose();
            }

            var cts = new CancellationTokenSource();
            _cts[guild.Id] = cts;

            var audio = await JoinAsync(user.VoiceChannel);
            if (audio == null || audio.ConnectionState != ConnectionState.Connected)
            {
                Console.WriteLine("❌ Cannot connect voice");
                return;
            }

            // 🚀 เล่นใน background
            _ = Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine("🎵 Resolving audio url...");

                    var videoId = await _youtube.ResolveVideoIdAsync(input);
                    var audioUrl = await _youtube.GetAudioOnlyUrlAsync(videoId);

                    Console.WriteLine("✅ Audio URL OK");

                    // 🎬 ffmpeg = ตัวแปลงเสียงหลัก
                    var psi = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments =
                            "-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 " +
                            "-headers \"User-Agent: Mozilla/5.0\r\n\" " +
                            $"-i \"{audioUrl}\" " +
                            "-vn -ac 2 -ar 48000 -f s16le pipe:1",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var ffmpeg = Process.Start(psi);
                    if (ffmpeg == null)
                    {
                        Console.WriteLine("❌ ffmpeg start failed");
                        return;
                    }

                    // 📜 log ffmpeg (debug เทพมาก)
                    _ = Task.Run(async () =>
                    {
                        while (!ffmpeg.StandardError.EndOfStream)
                        {
                            var line = await ffmpeg.StandardError.ReadLineAsync();
                            if (!string.IsNullOrWhiteSpace(line))
                                Console.WriteLine("[ffmpeg] " + line);
                        }
                    });

                    // 🔊 PCM stream → Discord
                    using var discord = audio.CreatePCMStream(AudioApplication.Music);

                    // 🗣 บอก Discord ว่า "กำลังพูดนะ"
                    await audio.SetSpeakingAsync(true);

                    await ffmpeg.StandardOutput.BaseStream.CopyToAsync(
                        discord, 32768, cts.Token
                    );

                    await discord.FlushAsync();
                    await audio.SetSpeakingAsync(false);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("⏹ Playback cancelled");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ PLAY ERROR");
                    Console.WriteLine(ex);
                }
                finally
                {
                    _cts.TryRemove(guild.Id, out _);
                }
            }, cts.Token);

            return;
        }
    }

    // ===== SKIP =====
    // ⏭ หยุดทุก guild
    public Task SkipAsync(ulong userId)
    {
        foreach (var kv in _cts)
        {
            kv.Value.Cancel();
            kv.Value.Dispose();
        }
        _cts.Clear();
        return Task.CompletedTask;
    }

    // ===== TOGGLE =====
    // 🔘 play / stop (ตอนนี้ = stop)
    public async Task ToggleAsync(ulong userId)
    {
        await SkipAsync(userId);
    }

    // ===== USERS IN VOICE =====
    // 👥 เอาไว้โชว์คนในห้อง voice (frontend-friendly)
    public async Task<object> GetUsersInVoice(ulong userId)
    {
        if (_readyTask != null)
            await _readyTask;

        if (_discordClient == null)
            return new { guild = "offline", users = new List<object>() };

        foreach (var g in _discordClient.Guilds)
        {
            var user = g.GetUser(userId);
            if (user?.VoiceChannel == null) continue;

            var channel = user.VoiceChannel;

            var users = g.Users
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
                guild = $"{g.Name} ({channel.Name})",
                users
            };
        }

        return new { guild = "not in voice", users = new List<object>() };
    }
}
