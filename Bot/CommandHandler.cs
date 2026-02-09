using Discord;
using Discord.WebSocket;
using DiscordMusicBot.Music;
using System.Linq;

namespace DiscordMusicBot.Bot;

public class CommandHandler
{
    private readonly DiscordSocketClient _client;
    private readonly MusicService _music;

    public CommandHandler(DiscordSocketClient client, MusicService music)
    {
        _client = client;
        _music = music;

        _client.Ready += RegisterCommandsAsync;
        _client.SlashCommandExecuted += HandleSlashCommandAsync;
    }

    private async Task RegisterCommandsAsync()
    {
        try
        {
            var helpCmd = new SlashCommandBuilder()
                .WithName("help")
                .WithDescription("ดูเมนูคำสั่งทั้งหมด");

            var joinCmd = new SlashCommandBuilder()
                .WithName("join")
                .WithDescription("สั่งให้บอทเข้าห้องเสียง");

            var playCmd = new SlashCommandBuilder()
                .WithName("play")
                .WithDescription("เล่นเพลงจาก YouTube")
                .AddOption(
                    name: "url",
                    type: ApplicationCommandOptionType.String,
                    description: "ชื่อเพลงหรือลิงก์ YouTube",
                    isRequired: true
                );

            var statusCmd = new SlashCommandBuilder()
                .WithName("status")
                .WithDescription("ดูสถานะปัจจุบัน");

            await _client.CreateGlobalApplicationCommandAsync(helpCmd.Build());
            await _client.CreateGlobalApplicationCommandAsync(joinCmd.Build());
            await _client.CreateGlobalApplicationCommandAsync(playCmd.Build());
            await _client.CreateGlobalApplicationCommandAsync(statusCmd.Build());

            Console.WriteLine("✅ Slash Commands registered successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error registering commands: {ex}");
        }
    }

    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        // 🔒 ป้องกัน null
        if (command.User is not SocketGuildUser user)
        {
            await command.RespondAsync("❌ คำสั่งนี้ใช้ได้เฉพาะในเซิร์ฟเวอร์", ephemeral: true);
            return;
        }

        var channel = user.VoiceChannel;

        try
        {
            switch (command.Data.Name)
            {
                case "help":
                    {
                        var embed = new EmbedBuilder()
                            .WithTitle("🎵 MrLastBot - เมนูคำสั่ง")
                            .WithDescription("เลือกใช้งานคำสั่งผ่านการพิมพ์ `/` ได้เลยครับ")
                            .WithColor(Color.Blue)
                            .AddField("🚀 พื้นฐาน",
                                "`/join` : เข้าห้องเสียง\n" +
                                "`/status` : ดูสถานะ")
                            .AddField("🎶 เพลง",
                                "`/play [url]` : เล่นเพลง YouTube")
                            .WithFooter("หากเมนูไม่ขึ้น ให้ลองปิด-เปิด Discord ใหม่")
                            .WithCurrentTimestamp()
                            .Build();

                        await command.RespondAsync(embed: embed);
                        break;
                    }

                case "join":
                    {
                        if (channel == null)
                        {
                            await command.RespondAsync(
                                "❌ คุณต้องเข้าห้องเสียงก่อนสั่งครับ",
                                ephemeral: true);
                            return;
                        }

                        var audioClient = await _music.JoinAsync(channel);
                        if (audioClient == null)
                        {
                            await command.RespondAsync("❌ ไม่สามารถเข้าห้องเสียงได้");
                            return;
                        }

                        await command.RespondAsync(
                            $"✅ เข้าไปที่ห้อง **{channel.Name}** เรียบร้อย!");
                        break;
                    }

                case "play":
                    {
                        if (channel == null)
                        {
                            await command.RespondAsync(
                                "❌ เข้าห้องเสียงก่อน",
                                ephemeral: true);
                            return;
                        }

                        var input = command.Data.Options
                            .First().Value.ToString()!;

                        await command.RespondAsync($"🎵 เพิ่มเพลงเข้าคิว: {input}");

                        await _music.PlayByUserIdAsync(user.Id, input);
                            user.Id,
                            input,
                            user.Username
                        );

                        break;
                    }


                case "status":
                    {
                        var statusObj = await _music.GetUsersInVoice(user.Id);

                        var guildInfo =
                            statusObj.GetType()
                                .GetProperty("guild")
                                ?.GetValue(statusObj)
                                ?.ToString()
                            ?? "unknown";

                        await command.RespondAsync(
                            $"📍 สถานะตอนนี้: **{guildInfo}**");
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"🔥 Command Error: {ex}");
            if (!command.HasResponded)
            {
                await command.RespondAsync(
                    "⚠️ เกิดข้อผิดพลาดในการประมวลผลคำสั่ง");
            }
        }
    }
}
