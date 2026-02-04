using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Diagnostics;


namespace DiscordMusicBot.Music;

public class MusicService
{
    private readonly YoutubeService _youtube = new();

    public async Task PlayAsync(IVoiceChannel channel, string url)
    {
        var audioClient = await channel.ConnectAsync();
        var stream = await _youtube.GetAudioStreamAsync(url);

        using var discord = audioClient.CreatePCMStream(AudioApplication.Music);
        await stream.CopyToAsync(discord);
        await discord.FlushAsync();
    }
}
