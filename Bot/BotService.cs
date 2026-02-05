public class BotService
{
    private readonly DiscordSocketClient _client;
    private readonly MusicService _music;

    public BotService(string token)
    {
        _client = new DiscordSocketClient();
        _music = new MusicService();
        _token = token;
    }

    public async Task StartAsync()
    {
        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();
    }

    // ===== เรียกจากเว็บ =====
    public async Task JoinFromWebAsync()
    {
        var guild = _client.Guilds.First();
        var channel = guild.VoiceChannels.First();
        await _music.JoinAndStayAsync(channel);
    }

    public async Task PlayFromWebAsync(string url)
    {
        await _music.PlayAsync(_music.LastChannel!, url);
    }
}
