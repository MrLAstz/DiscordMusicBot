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

    // ===== FIX libopus (Linux / Railway) =====
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

    // ===== JOIN =====
    public async Task<bool> JoinByUserIdAsync(ulong userId)
    {
        if (_discordClient == null) return false;

        foreach (var g in _discordClient.Guilds)
        {
            var u = g.GetUser(userId);
            if (u?.VoiceChannel != null)
            {
                await JoinAsync(u.VoiceChannel);
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
            if (_audioClients.TryGetValue(channel.Guild.Id, out var ex) &&
                ex.ConnectionState == ConnectionState.Connected)
                return ex;

            _audioClients.TryRemove(channel.Guild.Id, out IAudioClient _);

            var client = await channel.ConnectAsync(selfDeaf: true);

            client.Disconnected += _ =>
            {
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
    public async Task PlayByUserIdAsync(ulong userId, string url)
    {
        if (_discordClient == null) return;

        foreach (var g in _discordClient.Guilds)
        {
            var u = g.GetUser(userId);
            if (u?.VoiceChannel == null) continue;

            if (_cts.TryRemove(g.Id, out var old))
            {
                old.Cancel();
                old.Dispose();
            }

            var cts = new CancellationTokenSource();
            _cts[g.Id] = cts;

            var audio = await JoinAsync(u.VoiceChannel);
            if (audio == null) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    var streamUrl = await _youtube.GetAudioOnlyUrlAsync(url);

                    var psi = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments =
                            $"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 " +
                            $"-i \"{streamUrl}\" -ac 2 -ar 48000 -f s16le pipe:1",
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    };

                    using var ffmpeg = Process.Start(psi)!;
                    using var discord = audio.CreatePCMStream(AudioApplication.Music);

                    await ffmpeg.StandardOutput.BaseStream.CopyToAsync(
                        discord, 81920, cts.Token);

                    await discord.FlushAsync();
                }
                catch { }
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

        Console.WriteLine("❌ Voice not ready (timeout)");
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
