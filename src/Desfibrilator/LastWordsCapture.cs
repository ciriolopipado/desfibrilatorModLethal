using System;
using Dissonance;
using Dissonance.Audio.Capture;
using Desfibrilator.Core;
using NAudio.Wave;
using UnityEngine;

namespace Desfibrilator;

/// <summary>Uses Dissonance's processed local input; no second microphone, disk recordings or remote voice capture.</summary>
internal sealed class LastWordsCapture : IMicrophoneSubscriber, IDisposable
{
    private readonly object gate = new object();
    private readonly LastWordsBuffer buffer = new LastWordsBuffer();
    private DissonanceComms? comms;
    private volatile bool allowed, transmitting;
    private double phase;
    private int previousRate;
    private bool previouslyTransmitting;
    internal short[] Frozen { get; private set; } = Array.Empty<short>();

    internal void Update(bool consent, bool aliveInMission, bool replaying)
    {
        if (!consent) { Dispose(); return; }
        if (comms == null)
        {
            comms = UnityEngine.Object.FindObjectOfType<DissonanceComms>();
            if (comms != null) { comms.SubscribeToRecordedAudio(this); Plugin.Log("Ciriolator: local last-words capture enabled (memory only)."); }
        }
        allowed = aliveInMission && !replaying;
        // IsSpeaking alone may include transmission tails. Open channels and IsMuted also gate push-to-talk.
        var open = comms != null && (comms.RoomChannels.Count > 0 || comms.PlayerChannels.Count > 0);
        transmitting = allowed && comms != null && !string.IsNullOrEmpty(comms.LocalPlayerName) && !comms.IsMuted && open &&
            comms.FindPlayer(comms.LocalPlayerName)?.IsSpeaking == true;
        if (previouslyTransmitting && !transmitting) lock (gate) buffer.EndPhrase();
        previouslyTransmitting = transmitting;
    }

    internal void FreezeAtDeath()
    {
        allowed = transmitting = false;
        lock (gate) { Array.Clear(Frozen, 0, Frozen.Length); Frozen = buffer.Snapshot(); buffer.Clear(); }
    }

    public void ReceiveMicrophoneData(ArraySegment<float> input, WaveFormat format)
    {
        if (!allowed || input.Array == null || format.Channels <= 0 || format.Channels > 8 ||
            format.SampleRate < LastWordsBuffer.SampleRate || format.SampleRate > 192000) return;
        lock (gate)
        {
            if (!allowed) return;
            if (previousRate != format.SampleRate) { phase = 0; previousRate = format.SampleRate; }
            var frames = input.Count / format.Channels;
            if (frames <= 0 || frames > format.SampleRate) return;
            var samples = new short[(int)Math.Ceiling((double)frames * LastWordsBuffer.SampleRate / format.SampleRate) + 1];
            double energy = 0;
            var count = 0;
            var sendAudio = transmitting;
            for (var frame = 0; frame < frames; frame++)
            {
                float mono = 0;
                if (sendAudio)
                {
                    for (var c = 0; c < format.Channels; c++) mono += input.Array[input.Offset + frame * format.Channels + c];
                    mono /= format.Channels;
                    if (float.IsNaN(mono) || float.IsInfinity(mono)) mono = 0;
                    mono = Math.Max(-1, Math.Min(1, mono));
                }
                energy += mono * mono;
                phase += LastWordsBuffer.SampleRate;
                if (phase >= format.SampleRate)
                {
                    phase -= format.SampleRate;
                    samples[count++] = (short)(mono * short.MaxValue);
                }
            }
            buffer.Append(samples, count, sendAudio && Math.Sqrt(energy / frames) >= .008);
        }
    }

    public void Reset() { lock (gate) { buffer.Clear(); phase = 0; previousRate = 0; } }
    internal void ClearRound()
    {
        allowed = transmitting = false;
        lock (gate) { buffer.Clear(); Array.Clear(Frozen, 0, Frozen.Length); Frozen = Array.Empty<short>(); phase = 0; }
        previouslyTransmitting = false;
    }
    public void Dispose()
    {
        allowed = transmitting = false;
        if (comms != null) { comms.UnsubscribeFromRecordedAudio(this); comms = null; }
        ClearRound();
    }
}
