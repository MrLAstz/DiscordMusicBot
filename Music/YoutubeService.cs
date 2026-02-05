using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Search;
using System.Diagnostics;
using System.Linq;

namespace DiscordMusicBot.Music;

public class YoutubeService
{
    private readonly YoutubeClient _youtube = new();

    // --- ส่วนที่เพิ่มใหม่: สำหรับดึงรายการวิดีโอไปแสดงบน UI ---
    public async Task<List<object>> SearchVideosAsync(string query, int limit = 12)
    {
        var results = new List<object>();
        var searchResults = _youtube.Search.GetVideosAsync(query);

        await foreach (var video in searchResults)
        {
            results.Add(new
            {
                title = video.Title,
                url = video.Url,
                // ดึงรูป Thumbnail ที่คุณภาพสูงที่สุด
                thumbnail = video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url,
                author = video.Author.Title,
                duration = video.Duration?.ToString(@"mm\:ss") ?? "00:00"
            });

            if (results.Count >= limit) break;
        }
        return results;
    }

    // --- Method GetAudioStreamAsync เดิมของคุณ ---
    public async Task<Stream> GetAudioStreamAsync(string input)
    {
        string videoUrl = input;
        if (!input.Contains("youtube.com") && !input.Contains("youtu.be"))
        {
            var searchResults = _youtube.Search.GetVideosAsync(input);
            await foreach (var result in searchResults)
            {
                videoUrl = result.Url;
                break;
            }
        }

        var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoUrl);
        var audioStreamInfo = manifest
            .GetAudioOnlyStreams()
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault(s => s.Container == Container.WebM || s.Container == Container.Mp4);

        if (audioStreamInfo == null) throw new Exception("❌ ไม่พบไฟล์เสียงที่เล่นได้");

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 -analyzeduration 0 -probesize 32 -i \"{audioStreamInfo.Url}\" -ac 2 -f s16le -ar 48000 -loglevel panic pipe:1",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        return process.StandardOutput.BaseStream;
    }
}