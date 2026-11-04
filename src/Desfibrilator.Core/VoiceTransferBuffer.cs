using System;

namespace Desfibrilator.Core;

/// <summary>Ordered bounded PCM chunks; rejects gaps, replays and oversized frames.</summary>
public sealed class VoiceTransferBuffer
{
    public const int ChunkSamples = 384;
    private readonly short[] samples;
    public int Count => samples.Length;
    public int Position { get; private set; }
    public bool Complete => Position == Count;
    public VoiceTransferBuffer(int count)
    {
        if (count <= 0 || count > LastWordsBuffer.MaxSamples) throw new ArgumentOutOfRangeException(nameof(count));
        samples = new short[count];
    }
    public bool Append(int offset, short[] chunk)
    {
        if (offset != Position || chunk.Length <= 0 || chunk.Length > ChunkSamples || chunk.Length > Count - Position) return false;
        Array.Copy(chunk, 0, samples, Position, chunk.Length); Position += chunk.Length; return true;
    }
    public short[] Snapshot()
    {
        if (!Complete) throw new InvalidOperationException("Incomplete voice transfer.");
        return (short[])samples.Clone();
    }
    public void Clear() { Array.Clear(samples, 0, samples.Length); Position = 0; }
}
