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
    private Task? _readyTask;

    // ===== FIX libopus (Linux / Docker) =====
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

    public void SetReadyTask(Task readyTask) => _readyTask = readyTask;
    public void SetDiscordClient(DiscordSocketClient client) => _discordClient = client;

    // ===== JOIN BY USER ID =====
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
    public async Task<IAudioClient?> JoinAsync(IVoiceChannel channel)
    {
        if (_readyTask != null)
            await _readyTask;

        await _joinLock.WaitAsync();
        try
        {
            if (_audioClients.TryGetValue(channel.Guild.Id, out var existing) &&
                existing.ConnectionState == ConnectionState.Connected)
            {
                return existing;
            }

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

    // ===== PLAY =====
    public async Task PlayByUserIdAsync(ulong userId, string input)
    {
        if (_discordClient == null) return;

        foreach (var guild in _discordClient.Guilds)
        {
            var user = guild.GetUser(userId);
            if (user?.VoiceChannel == null) continue;

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

            _ = Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine("🎵 Resolving audio url...");
                    var audioUrl = await _youtube.GetAudioOnlyUrlAsync(input);
                    Console.WriteLine("✅ Audio URL OK");

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

                    // 🔥🔥🔥 สำคัญที่สุด
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

    // ===== TOGGLE (PLAY / STOP) =====
    public async Task ToggleAsync(ulong userId)
    {
        await SkipAsync(userId);
    }


    // ===== USERS IN VOICE =====
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
