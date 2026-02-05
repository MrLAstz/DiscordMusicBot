using Discord.WebSocket;
using DiscordMusicBot.Music;

namespace DiscordMusicBot.Bot;

public class CommandHandler
{
    private readonly DiscordSocketClient _client;
    private readonly MusicService _music;

    public CommandHandler(DiscordSocketClient client)
    {
        _client = client;
        _music = new MusicService();

        _client.MessageReceived += HandleAsync;
    }

    private async Task HandleAsync(SocketMessage msg)
    {
        if (msg.Author.IsBot) return;

        try
        {
            if (msg.Author is not SocketGuildUser user)
                return;

            var channel = user.VoiceChannel;

            // !join
            if (msg.Content == "!join")
            {
                if (channel == null)
                {
                    await msg.Channel.SendMessageAsync("❌ เข้าห้องเสียงก่อน");
                    return;
                }

                await _music.JoinAndStayAsync(channel);
                await msg.Channel.SendMessageAsync("✅ บอท voice ห้องว่าง");
            }

            // !play <url>
            else if (msg.Content.StartsWith("!play "))
            {
                if (channel == null)
                {
                    await msg.Channel.SendMessageAsync("❌ เข้าห้องเสียงก่อน");
                    return;
                }

                var url = msg.Content.Replace("!play ", "").Trim();
                await _music.PlayAsync(channel, url);
            }

            // !leave
            else if (msg.Content == "!leave")
            {
                await _music.LeaveAsync();
                await msg.Channel.SendMessageAsync("👋 ออกจาก voice แล้ว");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"🔥 Command error: {ex}");
            await msg.Channel.SendMessageAsync("⚠️ เกิดข้อผิดพลาด แต่บอทยังไม่ล้ม");
        }
    }
}