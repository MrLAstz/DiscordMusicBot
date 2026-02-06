using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices; // เพิ่มสำหรับการโหลด Library บน Linux

namespace DiscordMusicBot.Music;

public class MusicService
{
    private readonly ConcurrentDictionary<ulong, IAudioClient> _audioClients = new();
    private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _cts = new();
    private DiscordSocketClient? _discordClient;
    private readonly YoutubeService _youtube = new();

    // ✅ ส่วนที่เพิ่มเพื่อแก้ปัญหาบอทหลุดบน Railway (Linux)
    static MusicService()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                // พยายามโหลดจากชื่อมาตรฐาน
                NativeLibrary.TryLoad("libopus.so", out _);
                NativeLibrary.TryLoad("libsodium.so", out _);
                Console.WriteLine("✅ [System]: Native Libraries linked successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [System]: Native Library load warning: {ex.Message}");
            }
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

        // เมื่อหลุดจากการเชื่อมต่อ ให้ลบออกจาก list
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
                                Console.WriteLine("[Warning]: AudioClient disconnected, reconnecting...");
                                await JoinAsync(user.VoiceChannel);
                                _audioClients.TryGetValue(guild.Id, out audioClient);
                            }

                            string streamUrl = await _youtube.GetAudioOnlyUrlAsync(url);
                            Console.WriteLine($"[Info]: Playing: {url}");

                            var psi = new ProcessStartInfo
                            {
                                FileName = "ffmpeg", // บน Linux ต้องพิมพ์เล็ก และไม่มี .exe
                                Arguments = $"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 " +
                                            $"-i \"{streamUrl}\" -ac 2 -f s16le -ar 48000 -loglevel warning pipe:1",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            using var process = Process.Start(psi);
                            if (process == null) return;

                            // ดักดู Error จาก FFmpeg
                            _ = Task.Run(async () => {
                                string error = await process.StandardError.ReadToEndAsync();
                                if (!string.IsNullOrEmpty(error)) Console.WriteLine($"[FFmpeg]: {error}");
                            });

                            using var discordStream = audioClient.CreatePCMStream(AudioApplication.Music);

                            try
                            {
                                await process.StandardOutput.BaseStream.CopyToAsync(discordStream, 16384, newCts.Token);
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
                            Console.WriteLine($"[CRITICAL ERROR]: {ex.GetType().Name} - {ex.Message}");
                            Console.WriteLine($"[StackTrace]: {ex.StackTrace}");
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
            return new { guild = "Connecting Discord...", users = new List<object>() };

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
            return new { guild = "Not in voice channel", users = new List<object>() };

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