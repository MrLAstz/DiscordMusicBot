using System.Collections.Concurrent;

namespace DiscordMusicBot.Music;

public class MusicQueue
{
    private readonly ConcurrentQueue<MusicTrack> _queue = new();

    public void Enqueue(MusicTrack track)
        => _queue.Enqueue(track);

    public bool TryDequeue(out MusicTrack? track)
        => _queue.TryDequeue(out track);

    public IReadOnlyList<MusicTrack> Snapshot()
        => _queue.ToList();
}
