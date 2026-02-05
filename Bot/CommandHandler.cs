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
        // ไม่ตอบโต้บอท และรับเฉพาะข้อความที่มีเนื้อหา
        if (msg.Author.IsBot || string.IsNullOrEmpty(msg.Content)) return;

        try
        {
            if (msg.Author is not SocketGuildUser user)
                return;

            var channel = user.VoiceChannel;
            var content = msg.Content.Trim();

            // --- คำสั่ง !join ---
            if (content == "!join")
            {
                if (channel == null)
                {
                    await msg.Channel.SendMessageAsync("❌ คุณต้องเข้าห้องเสียงก่อนสั่งผมครับ!");
                    return;
                }

                await _music.JoinAsync(channel);
                await msg.Channel.SendMessageAsync($"✅ เข้าไปที่ห้อง **{channel.Name}** เรียบร้อย!");
            }

            // --- คำสั่ง !play [URL] ---
            else if (content.StartsWith("!play "))
            {
                var url = content.Substring(6).Trim(); // ตัดคำว่า "!play " ออก

                if (string.IsNullOrEmpty(url))
                {
                    await msg.Channel.SendMessageAsync("❌ กรุณาใส่ลิงก์ YouTube ด้วยครับ เช่น `!play https://...` ");
                    return;
                }

                if (channel == null)
                {
                    await msg.Channel.SendMessageAsync("❌ เข้าห้องเสียงก่อนถึงจะฟังเพลงได้นะ!");
                    return;
                }

                await msg.Channel.SendMessageAsync($"🎵 กำลังดึงเพลงมาเล่นให้ฟังในห้อง **{channel.Name}**...");

                // เรียกใช้ PlayByUserIdAsync ที่เราแก้ไว้ก่อนหน้านี้
                await _music.PlayByUserIdAsync(user.Id, url);
            }

            // --- คำสั่ง !status (สำหรับเช็คสถานะจากแชท) ---
            else if (content == "!status")
            {
                var statusObj = await _music.GetUsersInVoice(user.Id);
                // ดึงค่า guild จาก dynamic object ที่เราเขียนไว้ใน MusicService
                var guildInfo = statusObj.GetType().GetProperty("guild")?.GetValue(statusObj, null);

                await msg.Channel.SendMessageAsync($"