using System.Collections.Concurrent;
using DiscordMusicBot.Music.Models;

namespace DiscordMusicBot.Music;

public class MusicQueue
{
    private readonly ConcurrentQueue<MusicTrack> _queue = new();

    public void Enqueue(MusicTrack track)
        => _queue.Enqueue(track);

    public bool TryDequeue(out MusicTrack? track)
        => _queue.TryDequeue(out track);

    public IReadOnlyList<MusicTrack> List()
        => _queue.ToList();

    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
    }
}
