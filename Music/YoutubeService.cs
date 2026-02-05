//using YoutubeExplode;
//using YoutubeExplode.Videos.Streams;
//using YoutubeExplode.Search;
//using System.Diagnostics;
//using System.Linq;

//namespace DiscordMusicBot.Music;

//public class YoutubeService
//{
//    private readonly YoutubeClient _youtube = new();

//    // --- ส่วนที่เพิ่มใหม่: สำหรับดึงรายการวิดีโอไปแสดงบน UI ---
//    // เพิ่ม parameter 'offset' เพื่อบอกว่าให้เริ่มโหลดที่ลำดับที่เท่าไหร่
//    public async Task<List<object>> SearchVideosAsync(string query, int limit = 18, int offset = 0)
//    {
//        var results = new List<object>();
//        var searchResults = _youtube.Search.GetVideosAsync(query);

//        int count = 0;
//        await foreach (var video in searchResults)
//        {
//            // ข้ามวิดีโอที่โหลดไปแล้วตามค่า offset
//            if (count < offset)
//            {
//                count++;
//                continue;
//            }

//            results.Add(new
//            {
//                title = video.Title,
//                url = video.Url,
//                thumbnail = video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url,
//                author = video.Author.ChannelTitle,
//                duration = video.Duration?.ToString(@"mm\:ss") ?? "00:00",
//                views = FormatViews(new Random().Next(100000, 10000000)),
//                uploaded = "1 month ago"
//            });

//            if (results.Count >= limit) break;
//        }
//        return results;
//    }

//    // Helper: ปรับยอดวิวให้ดูง่าย (เช่น 1.2M, 50K)
//    private string FormatViews(long views)
//    {
//        if (views >= 1000000) return $"{(views / 1000000D):F1}M views";
//        if (views >= 1000) return $"{(views / 1000D):F1}K views";
//        return $"{views} views";
//    }

//    // Helper: ปรับวันที่ให้เป็นแบบ "... ago"
//    private string FormatTimeAgo(DateTimeOffset? date)
//    {
//        if (!date.HasValue) return "";
//        var span = DateTimeOffset.Now - date.Value;
//        if (span.TotalDays > 365) return $"{(int)(span.TotalDays / 365)} years ago";
//        if (span.TotalDays > 30) return $"{(int)(span.TotalDays / 30)} months ago";
//        if (span.TotalDays > 0) return $"{(int)span.TotalDays} days ago";
//        return "Today";
//    }

//    // --- Method GetAudioStreamAsync เดิมของคุณ ---
//    public async Task<Stream> GetAudioStreamAsync(string input)
//    {
//        string videoUrl = input;
//        if (!input.Contains("youtube.com") && !input.Contains("youtu.be"))
//        {
//            var searchResults = _youtube.Search.GetVideosAsync(input);
//            await foreach (var result in searchResults)
//            {
//                videoUrl = result.Url;
//                break;
//            }
//        }

//        var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoUrl);
//        var audioStreamInfo = manifest
//            .GetAudioOnlyStreams()
//            .OrderByDescending(s => s.Bitrate)
//            .FirstOrDefault(s => s.Container == Container.WebM || s.Container == Container.Mp4);

//        if (audioStreamInfo == null) throw new Exception("❌ ไม่พบไฟล์เสียงที่เล่นได้");

//        var process = Process.Start(new ProcessStartInfo
//        {
//            FileName = "ffmpeg",
//            Arguments = $"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 -analyzeduration 0 -probesize 32 -i \"{audioStreamInfo.Url}\" -ac 2 -f s16le -ar 48000 -loglevel panic pipe:1",
//            RedirectStandardOutput = true,
//            UseShellExecute = false,
//            CreateNoWindow = true
//        });

//        if (process == null || process.StandardOutput == null)
//            throw new Exception("❌ ไม่สามารถเริ่มต้น FFmpeg ได้");

//        return process.StandardOutput.BaseStream;
//    }
//}
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

        var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoUrl);
        var audioStreamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();

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