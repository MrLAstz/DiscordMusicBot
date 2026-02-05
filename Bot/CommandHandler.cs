using Discord;
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

        // 1. ลงทะเบียนเหตุการณ์เมื่อบอทพร้อม เพื่อส่งรายชื่อคำสั่งไปให้ Discord
        _client.Ready += RegisterCommandsAsync;

        // 2. รับคำสั่ง Slash Command
        _client.SlashCommandExecuted += HandleSlashCommandAsync;
    }

    private async Task RegisterCommandsAsync()
    {
        try
        {
            // สร้างคำสั่ง /help
            var helpCmd = new SlashCommandBuilder()
                .WithName("help")
                .WithDescription("ดูเมนูคำสั่งทั้งหมด");

            // สร้างคำสั่ง /join
            var joinCmd = new SlashCommandBuilder()
                .WithName("join")
                .WithDescription("สั่งให้บอทเข้าห้องเสียง");

            // สร้างคำสั่ง /play [url]
            var playCmd = new SlashCommandBuilder()
                .WithName("play")
                .WithDescription("เล่นเพลงจาก YouTube")
                .AddOption("url", ApplicationCommandOptionType.String, "ชื่อเพลงหรือลิงก์ YouTube", isRequired: true);

            // สร้างคำสั่ง /status
            var statusCmd = new SlashCommandBuilder()
                .WithName("status")
                .WithDescription("ดูสถานะปัจจุบัน");

            // ส่งคำสั่งทั้งหมดไปที่ Discord (แบบ Global)
            await _client.CreateGlobalApplicationCommandAsync(helpCmd.Build());
            await _client.CreateGlobalApplicationCommandAsync(joinCmd.Build());
            await _client.CreateGlobalApplicationCommandAsync(playCmd.Build());
            await _client.CreateGlobalApplicationCommandAsync(statusCmd.Build());

            Console.WriteLine("✅ Slash Commands registered successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error registering commands: {ex.Message}");
        }
    }

    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        var user = command.User as SocketGuildUser;
        var channel = user?.VoiceChannel;

        try
        {
            switch (command.Data.Name)
            {
                case "help":
                    var embed = new EmbedBuilder()
                        .WithTitle("🎵 MrLastBot - เมนูคำสั่ง")
                        .WithDescription("เลือกใช้งานคำสั่งผ่านการพิมพ์ `/` ได้เลยครับ")
                        .WithColor(Color.Blue)
                        .AddField("🚀 พื้นฐาน", "`/join` : เข้าห้องเสียง\n`/status` : ดูสถานะ")
                        .AddField("🎶 เพลง", "`/play [url]` : เล่นเพลง YouTube")
                        .AddField("🌐 Dashboard", "[คลิกเพื่อเปิดหน้าเว็บควบคุม](https://your-app.railway.app)")
                        .WithFooter(f => f.Text = "หากเมนูไม่ขึ้น ให้ลองปิด-เปิด Discord ใหม่")
                        .WithCurrentTimestamp()
                        .Build();
                    await command.RespondAsync(embed: embed);
                    break;

                case "join":
                    if (channel == null)
                    {
                        await command.RespondAsync("❌ คุณต้องเข้าห้องเสียงก่อนสั่งครับ", ephemeral: true);
                        return;
                    }
                    await _music.JoinAsync(channel);
                    await command.RespondAsync($"✅ เข้าไปที่ห้อง **{channel.Name}** เรียบร้อย!");
                    break;

                case "play":
                    if (channel == null)
                    {
                        await command.RespondAsync("❌ เข้าห้องเสียงก่อนถึงจะฟังเพลงได้นะ", ephemeral: true);
                        return;
                    }
                    var url = command.Data.Options.First().Value.ToString();
                    await command.RespondAsync($"🎵 กำลังเริ่มเล่นเพลง: {url}");
                    await _music.PlayByUserIdAsync(user.Id, url!);
                    break;

                case "status":
                    var statusObj = await _music.GetUsersInVoice(user.Id);
                    var guildInfo = statusObj.GetType().GetProperty("guild")?.GetValue(statusObj, null);
                    await command.RespondAsync($"📍 สถานะตอนนี้: **{guildInfo}**");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"🔥 Command Error: {ex.Message}");
            if (!command.HasResponded)
                await command.RespondAsync("⚠️ เกิดข้อผิดพลาดในการประมวลผลคำสั่ง");
        }
    }
}