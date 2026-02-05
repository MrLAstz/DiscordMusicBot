using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Search; // เพิ่มตัวนี้
using System.Diagnostics;

namespace DiscordMusicBot.Music;

public class YoutubeService
{
    private readonly YoutubeClient _youtube = new(); // สร้างไว้ใช้ซ้ำช่วยประหยัด Memory

    public async Task<Stream> GetAudioStreamAsync(string input)
    {
        string videoUrl = input;

        // 1. ตรวจสอบว่าสิ่งที่ส่งมาเป็น URL หรือ คำค้นหา
        if (!input.Contains("youtube.com") && !input.Contains("youtu.be"))
        {
            // ค้นหาและเอาวิดีโอแรกออกมาโดยใช้ await foreach (วิธีมาตรฐานของ IAsyncEnumerable)
            var searchResults = _youtube.Search.GetVideosAsync(input);

            // วนลูปเอาแค่อันแรกแล้ว break ทันที
            await foreach (var video in searchResults)
            {
                videoUrl = video.Url;
                break;
            }

            if (string.IsNullOrEmpty(videoUrl) || videoUrl == input)
                throw new Exception("❌ ไม่พบวิดีโอที่ค้นหา");
        }

        // 2. ดึง Stream Manifest
        var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoUrl);

        // 3. เลือก Audio Bitrate สูงสุด
        var audioStreamInfo = manifest
            .GetAudioOnlyStreams()
            .OrderByDescending(s => s.Bitrate)
            .First();

        // 4. ใช้ FFmpeg แปลงเป็น PCM (เพิ่มคำสั่งกันกระตุกสำหรับ Railway)
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            // เพิ่ม -reconnect เพื่อให้สตรีมไม่หลุดง่ายๆ เมื่อรันบน Server
            Arguments = $"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 -i \"{audioStreamInfo.Url}\" -ac 2 -f s16le -ar 48000 -loglevel panic pipe:1",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process == null) throw new Exception("❌ ไม่สามารถรัน FFmpeg ได้");

        return process.StandardOutput.BaseStream;
    }
}