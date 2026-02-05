using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Diagnostics;

namespace DiscordMusicBot.Music;

public class MusicService
{
    private readonly YoutubeService _youtube = new();
    private IAudioClient? _audioClient;

    // เข้า voice แล้วค้างไว้
    public async Task JoinAsync(IVoiceChannel channel)
    {
        if (_audioClient != null)
            return;

        try
        {
            _audioClient = await channel.ConnectAsync(selfDeaf: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Join voice failed: {ex.Message}");
        }
    }

    // ออก voice
    public async Task LeaveAsync()
    {
        try
        {
            if (_audioClient != null)
            {
                await _audioClient.StopAsync();
                _audioClient = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Leave voice failed: {ex.Message}");
        }
    }

    // เล่นเพลง (ไม่หลุด)
    public async Task PlayAsync(IVoiceChannel channel, string url)
    {
        try
        {
            if (_audioClient == null)
            {
                await JoinAsync(channel);
            }

            var stream = await _youtube.GetAudioStreamAsync(url);

            using var discord = _audioClient!.CreatePCMStream(AudioApplication.Music);
            await stream.CopyToAsync(discord);
            await discord.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"🔥 Play error: {ex.Message}");
        }
    }
}
