using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DiscordMusicBot.Music;

public class MusicService
{
    private readonly ConcurrentDictionary<ulong, IAudioClient> _audioClients = new();
    private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _cts = new();
    private readonly SemaphoreSlim _joinLock = new(1, 1);

    private DiscordSocketClient? _discordClient;
    private readonly YoutubeService _youtube = new();

    // ===== FIX libopus (Linux / Docker / Railway) =====
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

    public void SetDiscordClient(DiscordSocketClient client)
        => _discordClient = client;

    // ===== JOIN BY USER =====
    public async Task<IAudioClient?> JoinAsync(IVoiceChannel channel)
    {
        await _joinLock.WaitAsync();
        try
        {
            // ✅ ถ้ามี client ที่ยัง connected อยู่ ใช้ตัวเดิม
            if (_audioClients.TryGetValue(channel.Guild.Id, out var existing) &&
                existing.ConnectionState == ConnectionState.Connected)
            {
                return existing;
            }

            // ✅ ล้าง session เก่าทิ้งให้หมด
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
            var client = await channel.ConnectAsync(selfDeaf: true);

            // 🔥 สำคัญมาก กัน session expired (4006)
            await Task.Delay(500);

            client.Disconnected += _ =>
            {
                Console.WriteLine("🔌 Voice disconnected");
                _audioClients.TryRemove(channel.Guild.Id, out IAudioClient _);
                return Task.CompletedTask;
            };

            _audioClients[channel.Guild.Id] = client;
            return client;
        }
        finally
        {
            _joinLock.Release();
        }
    }

    // ===== PLAY =====
    public async Task PlayByUserIdAsync(ulong userId, string input)
    {
        if (_discordClient == null) return;

        foreach (var g in _discordClient.Guilds)
        {
            var u = g.GetUser(userId);
            if (u?.VoiceChannel == null) continue;

            // stop old
            if (_cts.TryRemove(g.Id, out var old))
            {
                old.Cancel();
                old.Dispose();
            }

            var cts = new CancellationTokenSource();
            _cts[g.Id] = cts;

            var audio = await JoinAsync(u.VoiceChannel);
            if (audio == null) return;

            if (!await WaitForVoiceReady(audio))
            {
                Console.WriteLine("❌ Voice not ready");
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    // 🔥 ใช้ VIDEO URL เท่านั้น (ให้ ffmpeg จัดการเอง)
                    var audioUrl = await _youtube.GetAudioOnlyUrlAsync(input);

                    var psi = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments =
                            "-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 " +
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
                        var err = await ffmpeg.StandardError.ReadToEndAsync();
                        if (!string.IsNullOrWhiteSpace(err))
                            Console.WriteLine($"[ffmpeg] {err}");
                    });

                    using var discord = audio.CreatePCMStream(
                        AudioApplication.Music,
                        bitrate: 96000,
                        bufferMillis: 200);

                    try
                    {
                        await ffmpeg.StandardOutput.BaseStream.CopyToAsync(
                            discord, 32768, cts.Token);
                    }
                    finally
                    {
                        await discord.FlushAsync();
                        if (!ffmpeg.HasExited)
                            ffmpeg.Kill();
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Play error: {ex}");
                }
                finally
                {
                    _cts.TryRemove(g.Id, out _);
                }
            }, cts.Token);

            return;
        }
    }

    private async Task<bool> WaitForVoiceReady(IAudioClient client, int timeoutMs = 8000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (client.ConnectionState == ConnectionState.Connected)
                return true;

            await Task.Delay(200);
        }
        return false;
    }

    // ===== SKIP =====
    public async Task SkipAsync(ulong userId)
    {
        if (_discordClient == null) return;

        foreach (var g in _discordClient.Guilds)
        {
            if (_cts.TryRemove(g.Id, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
    }

    public Task ToggleAsync(ulong userId) => SkipAsync(userId);

    // ===== USERS IN VOICE =====
    public Task<object> GetUsersInVoice(ulong userId)
    {
        if (_discordClient == null)
            return Task.FromResult<object>(new { guild = "offline", users = new List<object>() });

        SocketGuildUser? user = null;
        SocketGuild? guild = null;

        foreach (var g in _discordClient.Guilds)
        {
            user = g.GetUser(userId);
            if (user != null)
            {
                guild = g;
                break;
            }
        }

        if (user?.VoiceChannel == null || guild == null)
            return Task.FromResult<object>(new { guild = "not in voice", users = new List<object>() });

        var channel = user.VoiceChannel;

        var users = guild.Users
            .Where(u => u.VoiceChannel?.Id == channel.Id)
            .Select(u => new
            {
                name = u.GlobalName ?? u.Username,
                avatar = u.GetAvatarUrl() ?? u.GetDefaultAvatarUrl(),
                status = u.Status.ToString().ToLower()
            })
            .ToList();

        return Task.FromResult<object>(new
        {
            guild = $"{guild.Name} ({channel.Name})",
            users
        });
    }
}
