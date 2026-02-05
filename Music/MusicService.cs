using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Diagnostics;

namespace DiscordMusicBot.Music;

public class MusicService
{
    private IVoiceChannel? _lastChannel;
    private IAudioClient? _client;
    private readonly YoutubeService _youtube = new();

    public async Task JoinAsync(IVoiceChannel channel)
    {
        _lastChannel = channel;
        _client ??= await channel.ConnectAsync();
    }

    public async Task JoinLastAsync()
    {
        if (_lastChannel != null && _client == null)
            _client = await _lastChannel.ConnectAsync();
    }

    public async Task PlayAsync(IVoiceChannel channel, string url)
    {
        await JoinAsync(channel);
        await PlayInternal(url);
    }

    public async Task PlayLastAsync(string url)
    {
        if (_client == null) return;
        await PlayInternal(url);
    }

    private async Task PlayInternal(string url)
    {
        var stream = await _youtube.GetAudioStreamAsync(url);
        using var discord = _client!.CreatePCMStream(AudioApplication.Music);
        await stream.CopyToAsync(discord);
        await discord.FlushAsync();
    }
}