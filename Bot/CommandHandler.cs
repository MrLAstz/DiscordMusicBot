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
        var helpCmd = new SlashCommandBuilder()
            .WithName("help")
            .WithDescription("ดูเมนูคำสั่งทั้งหมด");

        var playCmd = new SlashCommandBuilder()
            .WithName("play")
            .WithDescription("เล่นเพลงจาก YouTube")
            .AddOption(
                "query",
                ApplicationCommandOptionType.String,
                "ชื่อเพลงหรือลิงก์ YouTube",
                isRequired: true
            );

        var statusCmd = new SlashCommandBuilder()
            .WithName("status")
            .WithDescription("ดูสถานะปัจจุบัน");

        await _client.CreateGlobalApplicationCommandAsync(helpCmd.Build());
        await _client.CreateGlobalApplicationCommandAsync(playCmd.Build());
        await _client.CreateGlobalApplicationCommandAsync(statusCmd.Build());

        Console.WriteLine("✅ Slash Commands registered");
    }

    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        if (command.User is not SocketGuildUser user)
        {
            await command.RespondAsync(
                "❌ ใช้ได้เฉพาะในเซิร์ฟเวอร์",
                ephemeral: true
            );
            return;
        }

        try
        {
            switch (command.Data.Name)
            {
                case "help":
                    {
                        var embed = new EmbedBuilder()
                            .WithTitle("🎵 MrLastBot")
                            .WithDescription("คำสั่งที่ใช้งานได้")
                            .AddField("🎶 เพลง", "`/play <ชื่อเพลง | url>`")
                            .AddField("📡 สถานะ", "`/status`")
                            .WithColor(Color.Blue)
                            .Build();

                        await command.RespondAsync(embed: embed);
                        break;
                    }

                case "play":
                    {
                        if (user.VoiceChannel == null)
                        {
                            await command.RespondAsync(
                                "❌ เข้าห้องเสียงก่อนนะ",
                                ephemeral: true
                            );
                            return;
                        }

                        var input = command.Data.Options.First().Value.ToString();
                        if (string.IsNullOrWhiteSpace(input))
                        {
                            await command.RespondAsync("❌ ต้องใส่ชื่อเพลงหรือ URL");
                            return;
                        }

                        await _music.EnqueueAsync(
                            user.Id,
                            input,
                            user.Username
                        );

                        await command.RespondAsync($"🎵 เพิ่มเข้าคิว: **{input}**");
                        break;
                    }

                case "status":
                    {
                        var status = await _music.GetUsersInVoice(user.Id);
                        await command.RespondAsync($"📍 {status}");
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("🔥 Command error");
            Console.WriteLine(ex);

            if (!command.HasResponded)
                await command.RespondAsync("⚠️ เกิดข้อผิดพลาด");
        }
    }
}
