using System;

namespace Desfibrilator.Core;

public readonly struct Attempt : IEquatable<Attempt>
{
    public readonly ulong Actor, Tool, Corpse;
    public Attempt(ulong actor, ulong tool, ulong corpse) { Actor = actor; Tool = tool; Corpse = corpse; }
    public bool Equals(Attempt other) => Actor == other.Actor && Tool == other.Tool && Corpse == other.Corpse;
    public override bool Equals(object? other) => other is Attempt attempt && Equals(attempt);
    public override int GetHashCode() => Actor.GetHashCode() ^ Tool.GetHashCode() ^ Corpse.GetHashCode();
}

/// <summary>Single server-owned channel and session-wide cooldown. All times are monotonic seconds.</summary>
public sealed class ReviveCoordinator
{
    public const double ChargeSeconds = 5, CooldownSeconds = 90, HeartbeatTimeout = 0.45;
    public Attempt? Active { get; private set; }
    private double startedAt, lastPulse, availableAt;
    public double CooldownRemaining(double now) => Math.Max(0, availableAt - now);
    public double ChargeRemaining(double now) => Active.HasValue ? Math.Max(0, ChargeSeconds - (now - startedAt)) : ChargeSeconds;

    public bool Pulse(Attempt attempt, double now)
    {
        if (CooldownRemaining(now) > 0) return false;
        if (Active.HasValue && now - lastPulse > HeartbeatTimeout) Active = null;
        if (Active.HasValue && !Active.Value.Equals(attempt)) return false;
        if (!Active.HasValue) { Active = attempt; startedAt = now; }
        lastPulse = now;
        return true;
    }

    public Attempt? Tick(double now, Func<Attempt, bool> validate)
    {
        if (!Active.HasValue) return null;
        if (now - lastPulse > HeartbeatTimeout || !validate(Active.Value)) { Active = null; return null; }
        return ChargeRemaining(now) <= 0 ? Active : null;
    }

    public void Cancel(ulong actor) { if (Active?.Actor == actor) Active = null; }

    // Called only after the game adapter has successfully restored the selected player.
    public void Complete(double now)
    {
        if (!Active.HasValue || ChargeRemaining(now) > 0 || now - lastPulse > HeartbeatTimeout)
            throw new InvalidOperationException("Cannot complete an interrupted or unfinished reanimation.");
        Active = null;
        availableAt = now + CooldownSeconds;
    }

    public void ResetSession() { Active = null; availableAt = 0; }
}
