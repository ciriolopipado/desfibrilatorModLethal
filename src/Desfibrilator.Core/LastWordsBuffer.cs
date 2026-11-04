using System;

namespace Desfibrilator.Core;

/// <summary>Bounded mono PCM buffer. Keeps the last utterance, trims silence and never retains a whole session.</summary>
public sealed class LastWordsBuffer
{
    public const int SampleRate = 8000, MaxSamples = SampleRate * 5;
    private const int SilenceLimit = SampleRate * 4 / 5, MinimumSpeech = SampleRate / 4;
    private readonly short[] ring = new short[MaxSamples + SilenceLimit];
    private short[] lastPhrase = Array.Empty<short>();
    private long written, lastSpeechEnd;
    private int silence, speechSamples;
    private bool active;

    public void Append(short[] samples, int count, bool speech)
    {
        if (count < 0 || count > samples.Length) throw new ArgumentOutOfRangeException(nameof(count));
        if (speech && !active) { active = true; written = lastSpeechEnd = 0; silence = speechSamples = 0; }
        if (!active) return;
        for (var i = 0; i < count; i++)
        {
            // Never retain untransmitted microphone content: silence is explicitly zeroed.
            ring[written++ % ring.Length] = speech ? samples[i] : (short)0;
            if (speech) { lastSpeechEnd = written; speechSamples++; silence = 0; }
            else if (++silence >= SilenceLimit) { EndPhrase(); break; }
        }
    }

    public void EndPhrase()
    {
        if (!active) return;
        if (speechSamples >= MinimumSpeech)
        {
            var phrase = Extract();
            Array.Clear(lastPhrase, 0, lastPhrase.Length);
            lastPhrase = phrase;
        }
        active = false; written = lastSpeechEnd = 0; silence = speechSamples = 0;
        Array.Clear(ring, 0, ring.Length);
    }

    public short[] Snapshot() => active && speechSamples >= MinimumSpeech ? Extract() : (short[])lastPhrase.Clone();

    private short[] Extract()
    {
        var count = (int)Math.Min(lastSpeechEnd, MaxSamples);
        var result = new short[count];
        var start = lastSpeechEnd - count;
        for (var i = 0; i < count; i++) result[i] = ring[(start + i) % ring.Length];
        return result;
    }

    public void Clear()
    {
        Array.Clear(ring, 0, ring.Length);
        Array.Clear(lastPhrase, 0, lastPhrase.Length);
        lastPhrase = Array.Empty<short>();
        active = false; written = lastSpeechEnd = 0; silence = speechSamples = 0;
    }
}
