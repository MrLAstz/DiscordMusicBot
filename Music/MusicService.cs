using Discord;
using Discord.Audio;
using Discord.WebSocket;

namespace DiscordMusicBot.Music;

public class MusicService
{
    private IAudioClient? _audioClient;
    private SocketVoiceChannel? _currentChannel;

    // ====== สำหรับ Web ======
    public async Task JoinFirstAvailableAsync()
    {
        if (_currentChannel != null) return;

        Console.WriteLine("⚠️ JoinFirstAvailableAsync ยังไม่มี guild context");
        await Task.CompletedTask;
    }

    public async Task PlayFromUrlAsync(string url)
    {
        Console.WriteLine($"🎵 Request play: {url}");
        await Task.CompletedTask;
    }

    // ====== สำหรับ Discord Command ======
    public async Task JoinAndStayAsync(SocketVoiceChannel channel)
    {
        if (_audioClient != null) return;

        _currentChannel = channel;
        _audioClient = await channel.ConnectAsync(selfDeaf: true);

        Console.WriteLine($"🔊 Joined voice: {channel.Name}");
    }
}
