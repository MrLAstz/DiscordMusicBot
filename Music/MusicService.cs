using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Diagnostics;

namespace DiscordMusicBot.Music;

public class MusicService
{
    private IAudioClient? _audioClient;
    private CancellationTokenSource? _keepAliveToken;

    private readonly YoutubeService _youtube = new();

    // เข้า voice แล้ว "ค้างถาวร"
    public async Task JoinAndStayAsync(IVoiceChannel channel)
    {
        if (_audioClient != null)
            return;

        _audioClient = await channel.ConnectAsync();
        Console.WriteLine("🎧 Joined voice");

        _keepAliveToken = new CancellationTokenSource();
        _ = Task.Run(() => KeepVoiceAlive(_keepAliveToken.Token));
    }

    // 🔇 ส่งเสียงเงียบกันโดนเตะ
    private async Task KeepVoiceAlive(CancellationToken token)
    {
        try
        {
            using var stream = _audioClient!.CreatePCMStream(AudioApplication.Mixed);
            var silence = new byte[3840]; // 20ms @ 48kHz

            while (!token.IsCancellationRequested)
            {
                await stream.WriteAsync(silence, 0, silence.Length, token);
                await Task.Delay(20, token);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"🔥 KeepAlive error: {ex.Message}");
        }
    }

    // ▶️ เล่นเพลง
    public async Task PlayAsync(IVoiceChannel channel, string url)
    {
        try
        {
            if (_audioClient == null)
                await JoinAndStayAsync(channel);

            var audioStream = await _youtube.GetAudioStreamAsync(url);

            using var discord = _audioClient!.CreatePCMStream(AudioApplication.Music);
            await audioStream.CopyToAsync(discord);
            await discord.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"🔥 Play error: {ex}");
        }
    }

    // 👋 ออกจาก voice (เฉพาะสั่งเอง)
    public async Task LeaveAsync()
    {
        try
        {
            _keepAliveToken?.Cancel();

            if (_audioClient != null)
            {
                await _audioClient.StopAsync();
                _audioClient = null;
            }

            Console.WriteLine("👋 Left voice");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"🔥 Leave error: {ex}");
        }
    }
}