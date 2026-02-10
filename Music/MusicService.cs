// 1. Using อยู่บนสุดเสมอ
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

// 2. ตามด้วย Namespace
namespace DiscordMusicBot.Music;

// 3. ตามด้วย Class
public class MusicService
{
    private readonly ConcurrentDictionary<ulong, IAudioClient> _audioClients = new();
    private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _cts = new();
    private readonly SemaphoreSlim _joinLock = new(1, 1);

    private DiscordSocketClient? _discordClient;
    private readonly YoutubeService _youtube = new();

    public MusicService()
    {
        // 🚩 วางตรงนี้แทนได้เหมือนกันครับ มันจะพิมพ์ออก Log ตอน Service นี้ถูกสร้าง
        Console.WriteLine($"🚀 Discord.Net Version: {Discord.DiscordConfig.Version}");
    }
    // ===== FIX libopus (Linux / Docker / Railway) =====
    static MusicService()
    {
        // แก้ไข: ใช้ DiscordConfig แทนการไปเรียก AudioClient โดยตรง
        var assembly = typeof(Discord.DiscordConfig).Assembly;
        Console.WriteLine("======================================");
        Console.WriteLine($"🔍 DLL Path: {assembly.Location}");
        Console.WriteLine($"🚀 Version: {Discord.DiscordConfig.Version}");
        Console.WriteLine("======================================");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            NativeLibrary.SetDllImportResolver(
                typeof(MusicService).Assembly,
                (libraryName, _, _) =>
                {
                    if (libraryName == "opus" || libraryName == "libopus")
                    {
                        var paths = new[] {
                        "libopus.so.0",
                        "libopus.so",
                        "/usr/lib/libopus.so",
                        "/usr/lib/x86_64-linux-gnu/libopus.so.0"
                        };
                        foreach (var p in paths)
                        {
                            if (NativeLibrary.TryLoad(p, out var h)) return h;
                        }
                    }
                    return IntPtr.Zero;
                });
        }
    }

    public void SetDiscordClient(DiscordSocketClient client)
        => _discordClient = client;

    // ===== JOIN BY USER ID =====
    public async Task<bool> JoinByUserIdAsync(ulong userId)
    {
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
            // ล้าง Client ใน Dictionary ของเรา
            if (_audioClients.TryRemove(channel.Guild.Id, out IAudioClient? existing))
            {
                try { await existing.StopAsync(); existing.Dispose(); } catch { }
                await Task.Delay(1000);
            }

            // 🔥 เพิ่มบรรทัดนี้: บังคับเตะตัวเองออกจาก Channel (เผื่อ Discord Server ยังจำว่าบอทอยู่ในห้อง)
            try { await channel.DisconnectAsync(); } catch { }
            await Task.Delay(2000); // รอให้ Gateway ลบ Session เก่าออกจริงๆ

            Console.WriteLine($"🔊 Creating Fresh Connection to {channel.Name}...");

            // เชื่อมต่อใหม่แบบใสสะอาด
            var client = await channel.ConnectAsync(selfDeaf: true, selfMute: false, external: false);

            // รอให้สถานะนิ่ง
            await Task.Delay(2000);

            if (client != null && client.ConnectionState == ConnectionState.Connected)
            {
                Console.WriteLine("✅ Fresh Voice Connected!");
                _audioClients[channel.Guild.Id] = client;
                return client;
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Join Error: {ex.Message}");
            return null;
        }
        finally
        {
            _joinLock.Release();
        }
    }
    // ===== PLAY (FIXED - NO RETRY LOOP TO AVOID 4006) =====
    public async Task PlayByUserIdAsync(ulong userId, string input)
    {
        if (_discordClient == null) return;

        // ค้นหา Guild ที่ User อยู่
        SocketGuildUser? user = null;
        foreach (var g in _discordClient.Guilds)
        {
            var u = g.GetUser(userId);
            if (u?.VoiceChannel != null)
            {
                user = u;
                break;
            }
        }

        if (user == null || user.VoiceChannel == null)
        {
            Console.WriteLine("❌ User not found in any voice channel.");
            return;
        }

        var guildId = user.Guild.Id;

        // 1️⃣ หยุดเพลงเก่าและยกเลิก Task เดิม
        if (_cts.TryRemove(guildId, out var old))
        {
            old.Cancel();
            old.Dispose();
        }

        var cts = new CancellationTokenSource();
        _cts[guildId] = cts;

        try
        {
            // 2️⃣ เชื่อมต่อเข้าห้องเสียง (เรียก JoinAsync เพียงครั้งเดียว)
            // 2️⃣ เชื่อมต่อเข้าห้องเสียง (เช็คก่อนว่าเชื่อมอยู่แล้วหรือไม่ เพื่อป้องกัน Error 4006)
            IAudioClient? audio;

            if (_audioClients.TryGetValue(user.Guild.Id, out var currentClient) &&
                currentClient.ConnectionState == ConnectionState.Connected)
            {
                // ถ้าเชื่อมต่ออยู่แล้ว ให้ใช้ตัวเดิมเล่นต่อเลย
                audio = currentClient;
                Console.WriteLine("♻️ Using existing connection to play music.");
            }
            else
            {
                // ถ้ายังไม่มีการเชื่อมต่อ หรือหลุดไปแล้ว ค่อยสั่ง Join ใหม่
                audio = await JoinAsync(user.VoiceChannel);
            }

            if (audio == null)
            {
                Console.WriteLine("❌ Cannot connect to voice channel.");
                return;
            }

            // 3️⃣ รอให้สถานะการเชื่อมต่อพร้อมใช้งาน
            if (!await WaitForVoiceReady(audio))
            {
                Console.WriteLine("❌ Voice ready timeout.");
                return;
            }

            // 4️⃣ เริ่มเล่นเพลงใน Background Task
            _ = Task.Run(async () =>
            {
                try
                {
                    // ค้นหา URL เสียงจาก YouTube
                    var audioUrl = await _youtube.GetAudioOnlyUrlAsync(input);

                    var psi = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = $"-hide_banner -loglevel error -i \"{audioUrl}\" -vn -ac 2 -ar 48000 -f s16le pipe:1",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var ffmpeg = Process.Start(psi);
                    if (ffmpeg == null) return;

                    using var discordStream = audio.CreatePCMStream(
                        AudioApplication.Music,
                        bitrate: 128000,
                        bufferMillis: 200
                    );

                    // 🔍 DEBUG & Buffer: ตรวจสอบข้อมูลจาก ffmpeg
                    var buffer = new byte[32768];
                    int bytesRead;

                    Console.WriteLine($"🎵 Starting playback: {input}");

                    // อ่านจาก FFmpeg และส่งไปยัง Discord จนกว่าจะจบหรือถูกยกเลิก
                    while (!cts.Token.IsCancellationRequested &&
                           (bytesRead = await ffmpeg.StandardOutput.BaseStream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
                    {
                        await discordStream.WriteAsync(buffer, 0, bytesRead, cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("⏹️ Playback stopped/skipped.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Playback error: {ex.Message}");
                }
                finally
                {
                    _cts.TryRemove(guildId, out _);
                    Console.WriteLine("🏁 Audio stream ended.");
                }
            }, cts.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"🔥 Global Play error: {ex.Message}");
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
        await Task.CompletedTask; // เพิ่มบรรทัดนี้เพื่อลบ Warning CS1998
    }


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