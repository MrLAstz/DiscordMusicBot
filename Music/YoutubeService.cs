using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Search;
using System.Diagnostics;
using System.Linq; // ✅ เพิ่มตัวนี้เพื่อให้ใช้ OrderByDescending และ First ได้มั่นใจขึ้น

namespace DiscordMusicBot.Music;

public class YoutubeService
{
    private readonly YoutubeClient _youtube = new();

    public async Task<Stream> GetAudioStreamAsync(string input)
    {
        string videoUrl = input;

        // 1. ตรวจสอบว่าเป็น URL หรือไม่ ถ้าไม่ใช่ให้ค้นหา
        if (!input.Contains("youtube.com") && !input.Contains("youtu.be"))
        {
            var searchResults = _youtube.Search.GetVideosAsync(input);
            await foreach (var result in searchResults) // ✅ ใช้ท่า await foreach อ่านง่ายและปลอดภัยกว่า
            {
                videoUrl = result.Url;
                break; // เอาแค่ผลลัพธ์แรกพอ
            }
        }

        // 2. ดึง Stream Manifest
        var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoUrl);

        // 3. เลือก Audio Stream (ปรับให้ปลอดภัยขึ้น)
        var audioStreamInfo = manifest
            .GetAudioOnlyStreams()
            .Where(s => s.Container == Container.WebM || s.Container == Container.Mp4) // ✅ กรองเฉพาะ Container ที่เสถียร
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault(); // ✅ ใช้ FirstOrDefault กันระเบิดถ้าไม่เจอ Stream

        if (audioStreamInfo == null) throw new Exception("❌ ไม่พบไฟล์เสียงที่เล่นได้");

        // 4. รัน FFmpeg
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            // ปรับ Arguments ให้กระชับและรองรับการสตรีมต่อเนื่อง
            Arguments = $"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 -i \"{audioStreamInfo.Url}\" -ac 2 -f s16le -ar 48000 -loglevel panic pipe:1",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process == null) throw new Exception("❌ ไม่สามารถรัน FFmpeg ได้");

        return process.StandardOutput.BaseStream;
    }
}