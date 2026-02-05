using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Search;
using System.Diagnostics;
using System.Linq;

namespace DiscordMusicBot.Music;

public class YoutubeService
{
    private readonly YoutubeClient _youtube = new();

    public async Task<Stream> GetAudioStreamAsync(string input)
    {
        string videoUrl = input;

        // 1. ค้นหาเพลง (ถ้าไม่ใช่ URL)
        if (!input.Contains("youtube.com") && !input.Contains("youtu.be"))
        {
            var searchResults = _youtube.Search.GetVideosAsync(input);
            await foreach (var result in searchResults)
            {
                videoUrl = result.Url;
                break;
            }
        }

        // 2. ดึง Stream Manifest
        var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoUrl);

        // 3. เลือก Audio Stream
        // ปรับ: เลือก Bitrate ที่เหมาะสม (ไม่จำเป็นต้องสูงสุดเสมอไปเพื่อให้โหลดเร็วเหมือน Spotify)
        var audioStreamInfo = manifest
            .GetAudioOnlyStreams()
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault(s => s.Container == Container.WebM || s.Container == Container.Mp4);

        if (audioStreamInfo == null) throw new Exception("❌ ไม่พบไฟล์เสียงที่เล่นได้");

        // 4. รัน FFmpeg
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            // ปรับ: เพิ่ม -analyzeduration และ -probesize 0 เพื่อให้ FFmpeg เริ่มทำงานทันทีโดยไม่ต้องรอวิเคราะห์ไฟล์นาน
            Arguments = $"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 -analyzeduration 0 -probesize 32 -i \"{audioStreamInfo.Url}\" -ac 2 -f s16le -ar 48000 -loglevel panic pipe:1",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process == null) throw new Exception("❌ ไม่สามารถรัน FFmpeg ได้");

        // คืนค่า Stream ของ FFmpeg ออกไปให้ MusicService
        return process.StandardOutput.BaseStream;
    }
}