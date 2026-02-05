using DiscordMusicBot.Bot;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

var token = builder.Configuration["DISCORD_TOKEN"];
if (string.IsNullOrEmpty(token))
{
    Console.WriteLine("❌ Missing DISCORD_TOKEN");
    return;
}

var app = builder.Build();

var bot = new BotService(token);
await bot.StartAsync();

// ===== API =====
app.MapGet("/", () => Results.File("wwwroot/index.html", "text/html"));

app.MapPost("/join", async () =>
{
    await bot.JoinFromWebAsync();
    return Results.Ok();
});

app.MapPost("/play", async (string url) =>
{
    await bot.PlayFromWebAsync(url);
    return Results.Ok();
});

app.Run();