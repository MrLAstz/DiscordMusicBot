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
    private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _playTokens = new();
    private readonly ConcurrentDictionary<ulong, MusicQueue> _queues = new();

    private readonly SemaphoreSlim _joinLock = new(1, 1);

    private DiscordSocketClient? _client;
    private Task? _readyTask;

    private readonly YoutubeService _youtube = new();

    // ===== FIX libopus (Docker / Linux) =====
    static MusicService()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            NativeLibrary.SetDllImportResolver(
                typeof(MusicService).Assembly,
                (name, _, _) =>
                {
                    if (name == "opus" || name == "libopus")
                    {
                        foreach (var p in new[] { "libopus.so.0", "libopus.so" })
                            if (NativeLibrary.TryLoad(p, out var h)) return h;
                    }
                    return IntPtr.Zero;
                });
        }
    }

    // ===== Inject =====
    public void SetDiscordClient(DiscordSocketClient client) => _client = client;
    public void SetReadyTask(Task task) => _readyTask = task;

    private MusicQueue GetQueue(ulong guildId)
        => _queues.GetOrAdd(guildId, _ => new MusicQueue());

    // ======================================================
    // 🎯 จุดเดียวที่รับเพลงจาก "ทุกที่" (web + /play)
    // ======================================================
    public async Task EnqueueAsync(ulong userId, string input, string source)
    {
        if (_client == null) return;
        if (_readyTask != null) await _readyTask;

        foreach (var guild in _client.Guilds)
        {
            var user = guild.GetUser(userId);
            if (user?.VoiceChannel == null) continue;

            var queue = GetQueue(guild.Id);

            queue.Enqueue(new MusicTrack
            {
                Input = input,
                RequestedBy = user.Username,
                Source = source
            });

            // ▶️ ถ้ายังไม่มี loop → start
            if (!_playTokens.ContainsKey(guild.Id))
            {
                _ = PlayerLoopAsync(guild, user.VoiceChannel);
            }
            return;
        }
    }


    // ======================================================
    // 🔁 Player Loop (1 guild = 1 loop เท่านั้น)
    // ======================================================
    private async Task PlayerLoopAsync(SocketGuild guild, IVoiceChannel channel)
    {
        var cts = new CancellationTokenSource();
        _playTokens[guild.Id] = cts;

        try
        {
            var audio = await JoinAsync(channel);
            if (audio == null) return;

            var queue = GetQueue(guild.Id);

            while (!cts.IsCancellationRequested &&
                   queue.TryDequeue(out var track))
            {
                await PlayFfmpegAsync(audio, track!, cts.Token);
            }
        }
        finally
        {
            _playTokens.TryRemove(guild.Id, out _);
        }
    }

    // ======================================================
    // 🔊 JOIN VOICE (ปลอดภัย ไม่ซ้อน)
    // ======================================================
    private async Task<IAudioClient?> JoinAsync(IVoiceChannel channel)
    {
        await _joinLock.WaitAsync();
        try
        {
            if (_audioClients.TryGetValue(channel.Guild.Id, out var existing) &&
                existing.ConnectionState == ConnectionState.Connected)
            {
                return existing;
            }

            if (_audioClients.TryRemove(channel.Guild.Id, out var old))
            {
                try { await old.StopAsync(); old.Dispose(); } catch { }
            }

            var client = await channel.ConnectAsync(selfMute: false, selfDeaf: false);

            var sw = Stopwatch.StartNew();
            while (client.ConnectionState != ConnectionState.Connected)
            {
                if (sw.ElapsedMilliseconds > 10_000)
                    return null;
                await Task.Delay(200);
            }

            _audioClients[channel.Guild.Id] = client;
            return client;
        }
        finally
        {
            _joinLock.Release();
        }
    }

    // ======================================================
    // 🎵 FFmpeg → Discord PCM
    // ======================================================
    private async Task PlayFfmpegAsync(IAudioClient audio, MusicTrack track, CancellationToken token)
    {
        try
        {
            var videoId = await _youtube.ResolveVideoIdAsync(track.Input);
            var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoId);
            var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();

            // ดึง Stream มาไว้ในตัวแปรก่อน
            using var youtubeStream = await _youtube.Videos.Streams.GetAsync(streamInfo);

            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg", // หรือ "/usr/bin/ffmpeg"
                Arguments = "-hide_banner -loglevel panic -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var ffmpeg = Process.Start(psi);
            if (ffmpeg == null) return;

            using var discord = audio.CreatePCMStream(AudioApplication.Music);
            await audio.SetSpeakingAsync(true);

            // Task สำหรับป้อนข้อมูลเข้า FFmpeg
            var fillFfmpegTask = youtubeStream.CopyToAsync(ffmpeg.StandardInput.BaseStream, token)
                .ContinueWith(_ => ffmpeg.StandardInput.Close()); // ต้องปิดไม่งั้น FFmpeg ไม่หยุดรอ

            // Task สำหรับดึงข้อมูลจาก FFmpeg ไป Discord
            var sendToDiscordTask = ffmpeg.StandardOutput.BaseStream.CopyToAsync(discord, token);

            await Task.WhenAny(fillFfmpegTask, sendToDiscordTask);
        }
        catch (Exception ex)
        {
            // แนะนำให้พ่น Error ออกมาดูใน Docker Logs
            Console.WriteLine($"[MusicService Error]: {ex.Message}");
        }
        finally
        {
            await audio.SetSpeakingAsync(false);
        }
    }

    // ======================================================
    // ⏭ SKIP
    // ======================================================
    public Task SkipAsync(ulong userId)
    {
        foreach (var kv in _playTokens)
        {
            kv.Value.Cancel();
            kv.Value.Dispose();
        }
        _playTokens.Clear();
        return Task.CompletedTask;
    }

    // ======================================================
    // 🔘 TOGGLE (ตอนนี้ = stop)
    // ======================================================
    public Task ToggleAsync(ulong userId)
        => SkipAsync(userId);

    public void TogglePause(ulong userId)
    {
        _player.Toggle();
    }

    // ======================================================
    // 👥 STATUS (frontend)
    // ======================================================
    public async Task<object> GetUsersInVoice(ulong userId)
    {
        if (_client == null) return new { guild = "offline", users = new List<object>() };
        if (_readyTask != null) await _readyTask;

        foreach (var g in _client.Guilds)
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
                });

            return new
            {
                guild = $"{g.Name} ({channel.Name})",
                users
            };
        }

        return new { guild = "not in voice", users = new List<object>() };
    }
}
