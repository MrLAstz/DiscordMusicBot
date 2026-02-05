using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Collections.Concurrent; // สำหรับจัดการ Dictionary ในหลาย Thread
using System.Linq;

namespace DiscordMusicBot.Music;

public class MusicService
{
    // 1. เปลี่ยนมาใช้ Dictionary เพื่อเก็บ ID ห้อง แยกตาม ID ของ Server (Guild)
    private readonly ConcurrentDictionary<ulong, ulong> _serverRooms = new();

    // 2. เก็บ Audio Client แยกตาม Server เพื่อให้เปิดเพลงพร้อมกันหลายที่ได้
    private readonly ConcurrentDictionary<ulong, IAudioClient> _audioClients = new();

    private DiscordSocketClient? _discordClient;
    private readonly YoutubeService _youtube = new();

    // ตัวแปรนี้จะกลายเป็น "ชื่อล่าสุด" ที่มีการเรียกใช้
    public string CurrentGuildName { get; set; } = "ไม่ได้เชื่อมต่อ";

    public void SetDiscordClient(DiscordSocketClient client)
    {
        _discordClient = client;
    }

    public async Task JoinAsync(IVoiceChannel channel)
    {
        // บันทึกว่า Server นี้ (Guild.Id) กำลังใช้ห้องไหน (channel.Id)
        _serverRooms[channel.Guild.Id] = channel.Id;

        if (!_audioClients.ContainsKey(channel.Guild.Id))
        {
            var audioClient = await channel.ConnectAsync();
            _audioClients[channel.Guild.Id] = audioClient;
        }
    }

    public async Task PlayAsync(IVoiceChannel channel, string url)
    {
        await JoinAsync(channel);

        var stream = await _youtube.GetAudioStreamAsync(url);
        if (_audioClients.TryGetValue(channel.Guild.Id, out var audioClient))
        {
            using var discord = audioClient.CreatePCMStream(AudioApplication.Music);
            await stream.CopyToAsync(discord);
            await discord.FlushAsync();
        }
    }

    // ✅ แก้ไข: ให้รับ guildId เพื่อดึงข้อมูลของ Server นั้นๆ
    public object GetStatus(ulong? guildId = null)
    {
        if (_discordClient == null)
            return new { guild = "บอทยังไม่พร้อม", users = new List<object>() };

        // ถ้าหน้าเว็บไม่ได้ระบุ guildId มา (เช่น หน้าแรก) ให้พยายามหยิบตัวแรกที่มี
        var targetGuildId = guildId ?? _serverRooms.Keys.FirstOrDefault();

        if (targetGuildId == 0 || !_serverRooms.TryGetValue(targetGuildId, out var channelId))
            return new { guild = "ไม่ได้เชื่อมต่อ", users = new List<object>() };

        try
        {
            var voiceChannel = _discordClient.GetChannel(channelId) as SocketVoiceChannel;
            if (voiceChannel == null)
                return new { guild = "ไม่พบห้อง", users = new List<object>() };

            return new
            {
                guild = voiceChannel.Guild.Name,
                users = voiceChannel.Users
                    .Where(u => u.VoiceChannel != null && u.VoiceChannel.Id == channelId)
                    .Select(u => new
                    {
                        name = u.GlobalName ?? u.Username,
                        avatar = u.GetAvatarUrl() ?? u.GetDefaultAvatarUrl(),
                        status = u.Status.ToString().ToLower()
                    }).ToList()
            };
        }
        catch
        {
            return new { guild = "Error", users = new List<object>() };
        }
    }
}