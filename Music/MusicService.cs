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

    static MusicService()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            NativeLibrary.SetDllImportResolver(typeof(MusicService).Assembly, (libraryName, assembly, searchPath) =>
            {
                if (libraryName == "opus" || libraryName == "libopus")
                {
                    // ลองหาในหลายๆ ชื่อที่ Linux อาจจะเรียก
                    string[] opusFiles = { "opus.so", "libopus.so", "libopus.so.0" };
                    foreach (var file in opusFiles)
                    {
                        if (NativeLibrary.TryLoad(Path.Combine(AppContext.BaseDirectory, file), out var handle)) return handle;
                        if (NativeLibrary.TryLoad(file, out handle)) return handle;
                    }
                }
                return IntPtr.Zero;
            });
            Console.WriteLine("🐧 [Audio] DllImportResolver registered for Linux.");
        }
    }

    public void SetDiscordClient(DiscordSocketClient client) => _discordClient = client;

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

    public async Task JoinAsync(IVoiceChannel channel)
    {
        var audioClient = await channel.ConnectAsync();
        audioClient.Disconnected += async (ex) => {
            _audioClients.TryRemove(channel.Guild.Id, out _);
        };
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
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            if (audioClient.ConnectionState != ConnectionState.Connected)
                            {
                                await JoinAsync(user.VoiceChannel);
                                _audioClients.TryGetValue(guild.Id, out audioClient);
                            }

                            string streamUrl = await _youtube.GetAudioOnlyUrlAsync(url);
                            Console.WriteLine($"[Info]: Playing: {url}");

                            var psi = new ProcessStartInfo
                            {
                                FileName = "ffmpeg",
                                Arguments = $"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 " +
                                            $"-i \"{streamUrl}\" -ac 2 -f s16le -ar 48000 -loglevel warning pipe:1",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            using var process = Process.Start(psi);
                            if (process == null) return;

                            _ = Task.Run(async () => {
                                string error = await process.StandardError.ReadToEndAsync();
                                if (!string.IsNullOrEmpty(error)) Console.WriteLine($"[FFmpeg]: {error}");
                            });

                            // ปรับ Bitrate และ Buffer ให้เหมาะสมกับ Linux Server
                            using var discordStream = audioClient.CreatePCMStream(AudioApplication.Music, bitrate: 96000, bufferMillis: 200);

                            try
                            {
                                // ใช้ BufferSize 32KB เพื่อความลื่นไหล
                                await process.StandardOutput.BaseStream.CopyToAsync(discordStream, 32768, newCts.Token);
                            }
                            finally
                            {
                                await discordStream.FlushAsync();
                                if (!process.HasExited) process.Kill();
                            }
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[CRITICAL ERROR]: {ex.Message}");
                        }
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

    public async Task ToggleAsync(ulong userId) => await SkipAsync(userId);

    public async Task SkipAsync(ulong userId)
    {
        if (_discordClient == null) return;
        foreach (var guild in _discordClient.Guilds)
        {
            var user = guild.GetUser(userId) ?? (await _discordClient.Rest.GetGuildUserAsync(guild.Id, userId) as IGuildUser);
            if (user?.VoiceChannel != null)
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
            return new { guild = "Connecting...", users = new List<object>() };

        SocketGuildUser? user = null;
        SocketGuild? targetGuild = null;

        foreach (var g in _discordClient.Guilds)
        {
            user = g.GetUser(userId);
            if (user != null) { targetGuild = g; break; }
        }

        if (user == null || targetGuild == null || user.VoiceChannel == null)
            return new { guild = "Not in voice", users = new List<object>() };

        var channel = user.VoiceChannel;
        var usersInRoom = targetGuild.Users
            .Where(u => u.VoiceChannel?.Id == channel.Id)
            .Select(u => new {
                name = u.GlobalName ?? u.Username,
                avatar = u.GetAvatarUrl() ?? u.GetDefaultAvatarUrl(),
                status = u.Status.ToString().ToLower()
            }).ToList();

        return new { guild = $"{targetGuild.Name} ({channel.Name})", users = usersInRoom };
    }
}