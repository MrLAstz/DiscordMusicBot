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

        // ถ้าส่งชื่อเพลงมา (ไม่ใช่ URL) ให้หา URL วิดีโอก่อน
        if (!input.Contains("youtube.com") && !input.Contains("youtu.be"))
        {
            var searchResults = _youtube.Search.GetVideosAsync(input);
            await foreach (var result in searchResults)
            {
                videoUrl = result.Url;
                break;
            }
        }

        // ตรงบรรทัดที่ดึง Manifest หากเจอปัญหาเล่นไม่ได้ ให้ลองแก้เป็น:
        var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoUrl);
        // เลือก audio stream ที่ไม่ใช่แบบ "DASH" บางประเภทที่มักจะมีปัญหา
        var audioStreamInfo = manifest.GetAudioOnlyStreams()
            .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.Mp3 || s.Container == YoutubeExplode.Videos.Streams.Container.WebM)
            .GetWithHighestBitrate();

        if (audioStreamInfo == null) throw new Exception("❌ ไม่พบไฟล์เสียงที่เล่นได้จาก YouTube API");

        return audioStreamInfo.Url; // คืนค่า URL สตรีมตรงๆ
    }

    private string FormatViews(long views)
    {
        if (views >= 1000000) return $"{(views / 1000000D):F1}M views";
        if (views >= 1000) return $"{(views / 1000D):F1}K views";
        return $"{views} views";
    }
}