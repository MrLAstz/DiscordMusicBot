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

    private DiscordSocketClient? _discordClient;
    private readonly YoutubeService _youtube = new();

    // ====== FIX libopus on Linux / Railway ======
    static MusicService()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            NativeLibrary.SetDllImportResolver(
                typeof(MusicService).Assembly,
                (libraryName, assembly, searchPath) =>
                {
                    if (libraryName == "opus" || libraryName == "libopus")
                    {
                        string[] paths =
                        {
                            "libopus.so.0",
                            "libopus.so",
                            "opus.so"
                        };

                        foreach (var path in paths)
                        {
                            if (NativeLibrary.TryLoad(path, out var handle))
                            {
                                Console.WriteLine($"✅ libopus loaded: {path}");
                                return handle;
                            }
                        }

                        Console.WriteLine("❌ libopus not found");
                    }

                    return IntPtr.Zero;
                });
        }
    }

    public void SetDiscordClient(DiscordSocketClient client)
        => _discordClient = client;

    // ====== JOIN BY USER ======
    public async Task<bool> JoinByUserIdAsync(ulong userId)
    {
        if (_discordClient == null) return false;

        foreach (var guild in _discordClient.Guilds)
        {
            var user =
                guild.GetUser(userId) ??
                await _discordClient.Rest.GetGuildUserAsync(guild.Id, userId) as IGuildUser;

            if (user?.VoiceChannel != null)
            {
                await JoinAsync(user.VoiceChannel);
                return true;
            }
        }

        return false;
    }

    // ====== JOIN VOICE (มีแค่อันเดียว) ======
    public async Task<IAudioClient?> JoinAsync(IVoiceChannel channel)
    {
        if (_audioClients.TryGetValue(channel.Guild.Id, out var existing) &&
            existing.ConnectionState == ConnectionState.Connected)
        {
            return existing;
        }

        _audioClients.TryRemove(channel.Guild.Id, out _);

        Console.WriteLine("🔊 Connecting to voice...");
        var audioClient = await channel.ConnectAsync(selfDeaf: true);

        audioClient.Disconnected += _ =>
        {
            Console.WriteLine("🔌 Voice disconnected");
            _audioClients.TryRemove(channel.Guild.Id, out _);
            return Task.CompletedTask;
        };

        _audioClients[channel.Guild.Id] = audioClient;
        return audioClient;
    }

    // ====== PLAY ======
    public async Task PlayByUserIdAsync(ulong userId, string url)
    {
        if (_discordClient == null) return;

        foreach (var guild in _discordClient.Guilds)
        {
            var user =
                guild.GetUser(userId) ??
                await _discordClient.Rest.GetGuildUserAsync(guild.Id, userId) as IGuildUser;

            if (user?.VoiceChannel == null) continue;

            if (_cts.TryRemove(guild.Id, out var oldCts))
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            var cts = new CancellationTokenSource();
            _cts[guild.Id] = cts;

            var audioClient = await JoinAsync(user.VoiceChannel);
            if (audioClient == null) return;

            await Task.Delay(500); // รอ voice websocket พร้อม

            _ = Task.Run(async () =>
            {
                try
                {
                    if (audioClient.ConnectionState != ConnectionState.Connected)
                        return;

                    string streamUrl = await _youtube.GetAudioOnlyUrlAsync(url);
                    Console.WriteLine($"▶ Playing: {streamUrl}");

                    var psi = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments =
                            $"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 " +
                            $"-i \"{streamUrl}\" " +
                            "-ac 2 -ar 48000 -f s16le -loglevel error pipe:1",
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

                    using var discord = audioClient.CreatePCMStream(
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
                    _cts.TryRemove(guild.Id, out _);
                }
            }, cts.Token);

            return;
        }
    }

    // ====== SKIP ======
    public async Task SkipAsync(ulong userId)
    {
        if (_discordClient == null) return;

        foreach (var guild in _discordClient.Guilds)
        {
            var user =
                guild.GetUser(userId) ??
                await _discordClient.Rest.GetGuildUserAsync(guild.Id, userId) as IGuildUser;

            if (user?.VoiceChannel == null) continue;

            if (_cts.TryRemove(guild.Id, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
    }

    public Task ToggleAsync(ulong userId) => SkipAsync(userId);

    // ====== USERS IN VOICE ======
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
