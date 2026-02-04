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

        if (msg.Content.StartsWith("!play "))
        {
            var url = msg.Content.Replace("!play ", "");
            var user = msg.Author as SocketGuildUser;
            var channel = user?.VoiceChannel;

            if (channel == null)
            {
                await msg.Channel.SendMessageAsync("❌ เข้าห้องเสียงก่อน");
                return;
            }

            await _music.PlayAsync(channel, url);
        }
    }
}
