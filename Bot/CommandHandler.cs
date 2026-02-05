using Discord.WebSocket;
using DiscordMusicBot.Music;

namespace DiscordMusicBot.Bot;

public class CommandHandler
{
    private readonly DiscordSocketClient _client;
    private readonly MusicService _music;

    public CommandHandler(DiscordSocketClient client, MusicService music)
    {
        _client = client;
        _music = music;

        _client.MessageReceived += HandleAsync;
    }

    private async Task HandleAsync(SocketMessage msg)
    {
        if (msg.Author.IsBot || string.IsNullOrEmpty(msg.Content)) return;

        try
        {
            if (msg.Author is not SocketGuildUser user)
                return;

            var channel = user.VoiceChannel;
            var content = msg.Content.Trim();

            if (content == "!join")
            {
                if (channel == null)
                {
                    await msg.Channel.SendMessageAsync("❌ คุณต้องเข้าห้องเสียงก่อนสั่งครับ");
                    return;
                }
                await _music.JoinAsync(channel);
                await msg.Channel.SendMessageAsync($"✅ เข้าไปที่ห้อง **{channel.Name}** แล้ว!");
            }
            else if (content.StartsWith("!play "))
            {
                var url = content.Substring(6).Trim();
                if (string.IsNullOrEmpty(url))
                {
                    await msg.Channel.SendMessageAsync("❌ ใส่ลิงก์ด้วยครับ เช่น `!play https://...` ");
                    return;
                }
                if (channel == null)
                {
                    await msg.Channel.SendMessageAsync("❌ เข้าห้องเสียงก่อนนะ!");
                    return;
                }
                await msg.Channel.SendMessageAsync("🎵 กำลังเริ่มเล่นเพลง...");
                await _music.PlayByUserIdAsync(user.Id, url);
            }
            else if (content == "!status")
            {
                var statusObj = await _music.GetUsersInVoice(user.Id);
                var guildInfo = statusObj.GetType().GetProperty("guild")?.GetValue(statusObj, null);
                await msg.Channel.SendMessageAsync($"📍 สถานะ: **{guildInfo}**");
            }
            else if (content == "!help")
            {
                // แก้ไขเครื่องหมายคำพูดและโครงสร้าง String ตรงนี้ครับ
                string helpText = "ℹ️ **คำสั่งที่มีตอนนี้:**\n" +
                                 "- `!join` : ให้บอทเข้าห้องเสียง\n" +
                                 "- `!play [URL]` : เล่นเพลงจาก YouTube\n" +
                                 "- `!status` : เช็คสถานะปัจจุบัน";
                await msg.Channel.SendMessageAsync(helpText);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"🔥 Command error: {ex.Message}");
        }
    }
}