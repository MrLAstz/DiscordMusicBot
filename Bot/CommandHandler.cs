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
        if (msg.Author.IsBot) return;

        try
        {
            if (msg.Author is not SocketGuildUser user)
                return;

            var channel = user.VoiceChannel;

            if (msg.Content == "!join")
            {
                if (channel == null)
                {
                    await msg.Channel.SendMessageAsync("❌ เข้าห้องเสียงก่อน");
                    return;
                }

                await _music.JoinAsync(channel);
                await msg.Channel.SendMessageAsync("✅ เข้า voice แล้ว");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"🔥 Command error: {ex}");
        }
    }
}
