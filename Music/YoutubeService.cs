using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System.Diagnostics;

namespace DiscordMusicBot.Music;

public class YoutubeService
{
    public async Task<Stream> GetAudioStreamAsync(string url)
    {
        var youtube = new YoutubeClient();

        var manifest = await youtube.Videos.Streams.GetManifestAsync(url);

        // ✅ เลือก audio bitrate สูงสุดแบบ manual
        var audioStreamInfo = manifest
            .GetAudioOnlyStreams()
            .OrderByDescending(s => s.Bitrate)
            .First();

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i \"{audioStreamInfo.Url}\" -ac 2 -f s16le -ar 48000 pipe:1",
            RedirectStandardOutput = true,
            UseShellExecute = false
        });

        return process!.StandardOutput.BaseStream;
    }
}
