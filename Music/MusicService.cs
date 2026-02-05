using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Diagnostics;

namespace DiscordMusicBot.Music;

public class MusicService
{
    private IAudioClient? _audioClient;
    private CancellationTokenSource? _keepAliveCts;

    // เข้า voice และอยู่ค้าง (ส่งเสียงเงียบ)
    public async Task JoinAndStayAsync(IVoiceChannel channel)
    {
        if (_audioClient != null)
            return;

        _audioClient = await channel.ConnectAsync();
        _keepAliveCts = new CancellationTokenSource();

        _ = Task.Run(() => KeepAliveAsync(_keepAliveCts.Token));
    }

    // loop ส่ง silence กันหลุด
    private async Task KeepAliveAsync(CancellationToken token)
    {
        try
        {
            if (_audioClient == null) return;

            using var stream = _audioClient.CreatePCMStream(AudioApplication.Mixed);
            byte[] silence = new byte[3840]; // 20ms PCM silence

            while (!token.IsCancellationRequested)
            {
                await stream.WriteAsync(silence, 0, silence.Length, token);
                await Task.Delay(1000, token);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"KeepAlive error: {ex.Message}");
        }
    }

    // เล่นเพลง (ใช้ connection เดิม)
    public async Task PlayAsync(IVoiceChannel channel, string url)
    {
        try
        {
            if (_audioClient == null)
                await JoinAndStayAsync(channel);

            if (_audioClient == null) return;

            using var process = CreateYoutubeProcess(url);
            using var output = process.StandardOutput.BaseStream;
            using var discord = _audioClient.CreatePCMStream(AudioApplication.Music);

            await output.CopyToAsync(discord);
            await discord.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Play error: {ex.Message}");
        }
    }

    // ออกจาก voice
    public async Task LeaveAsync()
    {
        try
        {
            _keepAliveCts?.Cancel();
            _keepAliveCts = null;

            if (_audioClient != null)
            {
                await _audioClient.StopAsync();
                _audioClient = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Leave error: {ex.Message}");
        }
    }

    // ใช้ yt-dlp + ffmpeg
    private Process CreateYoutubeProcess(string url)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = $"/C yt-dlp -f bestaudio -o - {url} | ffmpeg -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
    }
}