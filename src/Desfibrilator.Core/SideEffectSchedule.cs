using System;

namespace Desfibrilator.Core;

[Flags]
public enum SideEffectEvent { None = 0, Voice = 1, Shock = 2 }

public sealed class SideEffectSchedule
{
    public bool Active { get; private set; }
    public double NextVoice { get; private set; }
    public double NextShock { get; private set; }

    public void Activate(double now, Func<double> random)
    {
        Active = true;
        NextVoice = now + Interval(60, 120, random());
        NextShock = now + Interval(45, 90, random());
    }

    public SideEffectEvent Tick(double now, bool alive, bool hasVoice, Func<double> random)
    {
        if (!Active || !alive) return SideEffectEvent.None;
        var result = SideEffectEvent.None;
        if (now >= NextVoice)
        {
            if (hasVoice) result |= SideEffectEvent.Voice;
            NextVoice = now + Interval(60, 120, random());
        }
        if (now >= NextShock)
        {
            result |= SideEffectEvent.Shock;
            NextShock = now + Interval(45, 90, random());
        }
        return result;
    }

    public void Clear() { Active = false; NextVoice = NextShock = 0; }
    private static double Interval(double min, double max, double value) => min + (max - min) * Math.Max(0, Math.Min(1, value));
}
