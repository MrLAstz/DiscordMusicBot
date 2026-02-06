using DiscordMusicBot.Bot;
using DiscordMusicBot.Music;
using DiscordMusicBot.Web;
using System.Runtime.InteropServices; // ✅ เพิ่มตัวนี้เพื่อใช้จัดการเรื่องไฟล์ระบบ

class Program
{
    static async Task Main(string[] args)
    {
        // --- 🔧 เพิ่มโค้ดส่วนนี้เพื่อช่วยให้หา libopus เจอใน Linux (Railway) ---
        Console.WriteLine("🔧 Checking for Audio Libraries...");
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // พยายามโหลด libopus เผื่อไว้เพื่อให้ .NET รู้จักตำแหน่งไฟล์
            NativeLibrary.TryLoad("libopus", out _);
            NativeLibrary.TryLoad("libsodium", out _);
            Console.WriteLine("🐧 Linux environment detected, libraries pre-loaded.");
        }
        // ------------------------------------------------------------------

        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        // Railway จะส่งพอร์ตมาให้ผ่านตัวแปร PORT
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("❌ DISCORD_TOKEN not found");
            return;
        }

        var music = new MusicService();

        // ▶️ เปิดเว็บ (ส่ง port เข้าไปด้วย)
        _ = Task.Run(() => WebServer.Start(args, music, port));

        // ▶️ เปิดบอท
        var bot = new BotService(token, music);
        await bot.StartAsync();

        await Task.Delay(-1);
    }
}