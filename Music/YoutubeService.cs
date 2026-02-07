using YoutubeExplode;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace DiscordMusicBot.Music;

/// <summary>
/// 📺 YouTube backend
/// - search วิดีโอ
/// - resolve keyword / url
/// - ดึง audio-only stream ไปป้อน ffmpeg
/// </summary>
public class YoutubeService
{
    // 🚀 client หลักของ YoutubeExplode
    private readonly YoutubeClient _youtube = new();

    // 🎲 fake views เอาไปโชว์ UI
    private static readonly Random _rand = new();

    // ================= SEARCH =================
    // 🔍 ค้นวิดีโอจาก keyword
    public async Task<List<object>> SearchVideosAsync(string query, int limit = 18, int offset = 0)
    {
        var results = new List<object>();
        int skipped = 0;

        await foreach (var video in _youtube.Search.GetVideosAsync(query))
        {
            // ⏭ ข้ามตาม offset
            if (skipped++ < offset) continue;

            results.Add(new
            {
                title = video.Title,
                url = $"https://www.youtube.com/watch?v={video.Id}",
                thumbnail = video.Thumbnails
                    .OrderByDescending(t => t.Resolution.Area)
                    .FirstOrDefault()?.Url,
                author = video.Author.ChannelTitle,
                duration = video.Duration?.ToString(@"mm\:ss") ?? "00:00",
                views = FormatViews(_rand.Next(100_000, 10_000_000)),
                uploaded = "recent"
            });

            // 🧯 กันโหลดเกิน
            if (results.Count >= limit)
                break;
        }

        return results;
    }

    // ================= AUDIO STREAM =================
    // 🎧 เอา URL audio-only ไปให้ ffmpeg: -i "<url>"
    public async Task<string> GetAudioOnlyUrlAsync(string input)
    {
        // 🎯 resolve ให้ได้ video id ก่อน
        var videoId = await ResolveVideoIdAsync(input);

        // 📦 ดึง stream manifest
        var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoId);

        // 🎶 เลือก audio อย่างเดียว bitrate แรงสุด
        var audio = manifest
            .GetAudioOnlyStreams()
            .Where(s => s.Container == Container.WebM || s.Container == Container.Mp4)
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault();

        if (audio == null)
            throw new Exception("❌ ไม่พบ audio stream");

        // 🔥 URL นี้เอาไป pipe เข้า ffmpeg ได้ตรงๆ
        return audio.Url;
    }

    // ================= RESOLVE VIDEO =================
    // 🧠 รับได้ทั้ง YouTube URL และ keyword
    private async Task<VideoId> ResolveVideoIdAsync(string input)
    {
        // 🔗 ถ้าเป็นลิงก์ YouTube → parse ตรง
        if (input.Contains("youtube.com") || input.Contains("youtu.be"))
            return VideoId.Parse(input);

        // 🔍 ถ้าเป็นคำค้น → เอาวิดีโอแรก
        await foreach (var v in _youtube.Search.GetVideosAsync(input))
            return v.Id;

        throw new Exception("❌ ไม่พบวิดีโอ");
    }

    // ================= UTIL =================
    // 👁 แปลง view ให้อ่านง่าย
    private static string FormatViews(long views)
    {
        if (views >= 1_000_000) return $"{views / 1_000_000D:F1}M views";
        if (views >= 1_000) return $"{views / 1_000D:F1}K views";
        return $"{views} views";
    }
}
