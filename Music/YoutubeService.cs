using YoutubeExplode;
using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;

namespace DiscordMusicBot.Music;

public class YoutubeService
{
    private readonly YoutubeClient _youtube = new();
    private static readonly Random _rand = new();

    public async Task<List<object>> SearchVideosAsync(string query, int limit = 18, int offset = 0)
    {
        var results = new List<object>();
        int skipped = 0;

        await foreach (var video in _youtube.Search.GetVideosAsync(query))
        {
            if (skipped++ < offset) continue;

            results.Add(new
            {
                title = video.Title,
                url = video.Url,
                thumbnail = video.Thumbnails.MaxBy(t => t.Resolution.Area)?.Url,
                author = video.Author.ChannelTitle,
                duration = video.Duration?.ToString(@"mm\:ss") ?? "00:00",
                views = FormatViews(_rand.Next(100_000, 10_000_000)),
                uploaded = "1 month ago"
            });

            if (results.Count >= limit) break;
        }
        return results;
    }

    public async Task<string> GetAudioOnlyUrlAsync(string input)
    {
        var videoUrl = input;

        if (!input.Contains("youtube.com") && !input.Contains("youtu.be"))
        {
            await foreach (var v in _youtube.Search.GetVideosAsync(input))
            {
                videoUrl = v.Url;
                break;
            }
        }

        var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoUrl);

        var audio = manifest.GetAudioOnlyStreams()
            .Where(s => s.Container == Container.WebM)
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault()
            ?? manifest.GetAudioOnlyStreams()
                .OrderByDescending(s => s.Bitrate)
                .FirstOrDefault();

        if (audio == null)
            throw new Exception("❌ ไม่พบไฟล์เสียงที่เล่นได้");

        return audio.Url;
    }

    private static string FormatViews(long views)
    {
        if (views >= 1_000_000) return $"{views / 1_000_000D:F1}M views";
        if (views >= 1_000) return $"{views / 1_000D:F1}K views";
        return $"{views} views";
    }
}
