using System;
using System.Linq;
using System.Threading;
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
    private VoiceBroadcastTrigger[] triggers = Array.Empty<VoiceBroadcastTrigger>();
    private double refreshTriggersAt;
    private volatile bool failed;
    private string? captureError;
    internal short[] Frozen { get; private set; } = Array.Empty<short>();

    internal void Update(bool enabled, bool aliveInMission, bool replaying)
    {
        if (!enabled) { Dispose(); return; }
        var error = Interlocked.Exchange(ref captureError, null);
        if (error != null)
        {
            Plugin.Error($"Ciriolator voice capture disabled for this expedition; live voice was left untouched: {error}");
            Unsubscribe();
        }
        if (failed) return;
        if (comms == null)
        {
            comms = UnityEngine.Object.FindObjectOfType<DissonanceComms>();
            if (comms != null) { comms.SubscribeToRecordedAudio(this); Plugin.Log("Ciriolator: local last-words capture enabled (memory only)."); }
        }
        allowed = aliveInMission && !replaying;
        if (Time.realtimeSinceStartupAsDouble >= refreshTriggersAt)
        {
            triggers = UnityEngine.Object.FindObjectsOfType<VoiceBroadcastTrigger>();
            refreshTriggersAt = Time.realtimeSinceStartupAsDouble + 2;
        }
        // Observe the actual broadcast triggers, not the playback/spectator VoicePlayerState cache.
        var open = comms != null && (comms.RoomChannels.Count > 0 || comms.PlayerChannels.Count > 0);
        var broadcasting = triggers.Length == 0 ? open : triggers.Any(t => t != null && t.isActiveAndEnabled && !t.IsMuted && t.IsTransmitting);
        transmitting = allowed && comms != null && !comms.IsMuted && open && broadcasting;
        if (previouslyTransmitting && !transmitting) lock (gate) buffer.EndPhrase();
        previouslyTransmitting = transmitting;
    }

    internal void FreezeAtDeath()
    {
        allowed = transmitting = false;
        lock (gate) { Array.Clear(Frozen, 0, Frozen.Length); Frozen = buffer.Snapshot(); buffer.Clear(); }
        Plugin.Log($"Ciriolator last words frozen: {Frozen.Length} samples ({(double)Frozen.Length / LastWordsBuffer.SampleRate:0.00}s).");
    }

    public void ReceiveMicrophoneData(ArraySegment<float> input, WaveFormat format)
    {
        // A subscriber exception must never propagate into Dissonance's live microphone pipeline.
        try { Receive(input, format); }
        catch (Exception error)
        {
            failed = true; allowed = transmitting = false;
            Interlocked.CompareExchange(ref captureError, error.ToString(), null);
        }
    }

    private void Receive(ArraySegment<float> input, WaveFormat format)
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

    // A microphone/processing reset must not erase words already spoken earlier in this expedition.
    public void Reset()
    {
        try { lock (gate) { buffer.EndPhrase(); phase = 0; previousRate = 0; } }
        catch (Exception error) { failed = true; allowed = transmitting = false; Interlocked.CompareExchange(ref captureError, error.ToString(), null); }
    }
    internal void ClearRound()
    {
        allowed = transmitting = false;
        lock (gate) { buffer.Clear(); Array.Clear(Frozen, 0, Frozen.Length); Frozen = Array.Empty<short>(); phase = 0; }
        previouslyTransmitting = false;
        failed = false;
    }
    private void Unsubscribe()
    {
        allowed = transmitting = false;
        if (comms != null) { comms.UnsubscribeFromRecordedAudio(this); comms = null; }
        triggers = Array.Empty<VoiceBroadcastTrigger>(); refreshTriggersAt = 0;
    }
    public void Dispose()
    {
        Unsubscribe();
        ClearRound();
    }
}
