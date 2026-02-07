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
    private Task _readyTask = Task.CompletedTask;

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

    public void SetReadyTask(Task readyTask)
    {
        _readyTask = readyTask;
    }

    public void SetDiscordClient(DiscordSocketClient client)
        => _discordClient = client;

    // ===== JOIN BY USER ID =====
    public async Task<bool> JoinByUserIdAsync(ulong userId)
    {
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
        await _joinLock.WaitAsync();
        try
        {
            // 1️⃣ ถ้ามี client ที่ยังใช้ได้ → ใช้ต่อ
            if (_audioClients.TryGetValue(channel.Guild.Id, out IAudioClient existing) &&
                existing.ConnectionState == ConnectionState.Connected)
            {
                return existing;
            }

            // 2️⃣ ปิด session เก่าแบบ "รอจริง"
            if (_audioClients.TryRemove(channel.Guild.Id, out IAudioClient? old))
            {
                try
                {
                    await old.StopAsync();
                    await Task.Delay(300); // สำคัญมาก
                    old.Dispose();
                }
                catch { }
            }

            Console.WriteLine("🔊 Connecting voice...");

            // 3️⃣ Connect แบบ safe
            var client = await channel.ConnectAsync(
                selfDeaf: false,
                selfMute: false
            );


            // 4️⃣ รอ Discord sync voice state
            await Task.Delay(800);

            client.Disconnected += _ =>
            {
                Console.WriteLine("🔌 Voice disconnected");

                _audioClients.TryRemove(
                    channel.Guild.Id,
                    out IAudioClient? oldClient
                );

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
        try
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

                IAudioClient? audio = null;

                for (int i = 0; i < 3; i++)
                {
                    audio = await JoinAsync(u.VoiceChannel);

                    if (audio?.ConnectionState == ConnectionState.Connected)
                        break;

                    Console.WriteLine($"⏳ Voice retry {i + 1}/3");
                    await Task.Delay(1000);
                }

                if (audio == null || audio.ConnectionState != ConnectionState.Connected)
                {
                    Console.WriteLine("❌ Cannot connect voice");
                    return;
                }

                if (!await WaitForVoiceReady(audio))
                {
                    Console.WriteLine("❌ Voice not ready");
                    return;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        Console.WriteLine("🎵 Resolving audio url...");
                        var audioUrl = await _youtube.GetAudioOnlyUrlAsync(input);
                        Console.WriteLine($"✅ Audio URL OK");

                        var psi = new ProcessStartInfo
                        {
                            FileName = "ffmpeg",
                            Arguments =
                                "-hide_banner -loglevel error " +
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

                        using var discord = audio.CreatePCMStream(
                            AudioApplication.Music,
                            bitrate: 128000,
                            bufferMillis: 200
                        );

                        await ffmpeg.StandardOutput.BaseStream.CopyToAsync(
                            discord, 32768, cts.Token
                        );

                        await discord.FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("❌ PLAY TASK ERROR");
                        Console.WriteLine(ex);
                    }
                    finally
                    {
                        _cts.TryRemove(g.Id, out _);
                    }
                }, cts.Token);

                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ PlayByUserIdAsync ERROR");
            Console.WriteLine(ex);
            throw; // สำคัญ: ให้ ASP.NET log stacktrace เต็ม
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

    public async Task ToggleAsync(ulong userId)
    {
        await SkipAsync(userId);
    }


    // ===== USERS IN VOICE =====
    public async Task<object> GetUsersInVoice(ulong userId)
    {
        await _readyTask;

        if (_discordClient == null)
            return new { guild = "offline", users = new List<object>() };

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
            return new { guild = "not in voice", users = new List<object>() };

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

        return new
        {
            guild = $"{guild.Name} ({channel.Name})",
            users
        };
    }
}
