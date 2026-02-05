using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Search;
using System.Diagnostics;
using System.Linq;

namespace DiscordMusicBot.Music;

public class YoutubeService
{
    private readonly YoutubeClient _youtube = new();

    // สำหรับดึงรายการวิดีโอไปแสดงบน UI
    public async Task<List<object>> SearchVideosAsync(string query, int limit = 18, int offset = 0)
    {
        var results = new List<object>();
        var searchResults = _youtube.Search.GetVideosAsync(query);

        int count = 0;
        await foreach (var video in searchResults)
        {
            if (count < offset)
            {
                count++;
                continue;
            }

            results.Add(new
            {
                title = video.Title,
                url = video.Url,
                thumbnail = video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url,
                author = video.Author.ChannelTitle,
                duration = video.Duration?.ToString(@"mm\:ss") ?? "00:00",
                views = FormatViews(new Random().Next(100000, 10000000)),
                uploaded = "1 month ago"
            });

            if (results.Count >= limit) break;
        }
        return results;
    }

    // จุดสำคัญ: ดึง Direct URL ของสตรีมเสียงจาก YouTube API
    public async Task<string> GetAudioOnlyUrlAsync(string input)
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

        // กรองเอา Opus หรือ WebM เพราะ Discord ใช้ Opus เป็นหลัก จะทำให้การถอดรหัสลื่นที่สุด
        var audioStreamInfo = manifest.GetAudioOnlyStreams()
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault();

        if (audioStreamInfo == null) throw new Exception("❌ ไม่พบไฟล์เสียงที่เล่นได้");

        return audioStreamInfo.Url;
    }

    private string FormatViews(long views)
    {
        if (views >= 1000000) return $"{(views / 1000000D):F1}M views";
        if (views >= 1000) return $"{(views / 1000D):F1}K views";
        return $"{views} views";
    }
}