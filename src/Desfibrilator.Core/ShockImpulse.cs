using System;

namespace Desfibrilator.Core;

/// <summary>One server-selected shock, shared by every observer.</summary>
public readonly struct ShockImpulse
{
    public const double WalkSeconds = .75;
    public float Yaw { get; }
    public float Pitch { get; }
    public bool Convulse { get; }
    public float DirectionDegrees { get; }

    public ShockImpulse(float yaw, float pitch, bool convulse, float directionDegrees)
    { Yaw = yaw; Pitch = pitch; Convulse = convulse; DirectionDegrees = directionDegrees; }

    public static ShockImpulse Create(Func<double> random) => new ShockImpulse(
        Signed(90, 180, random), Signed(2, 6, random), Unit(random()) < .5, (float)(360 * Unit(random())));

    public bool IsValid => Finite(Yaw) && Math.Abs(Yaw) >= 90 && Math.Abs(Yaw) <= 180 &&
        Finite(Pitch) && Math.Abs(Pitch) >= 2 && Math.Abs(Pitch) <= 6 &&
        Finite(DirectionDegrees) && DirectionDegrees >= 0 && DirectionDegrees <= 360;

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static double Unit(double value) => double.IsNaN(value) ? 0 : Math.Max(0, Math.Min(1, value));
    private static float Signed(float min, float max, Func<double> random) =>
        (float)(min + (max - min) * Unit(random())) * (Unit(random()) < .5 ? -1 : 1);
}

/// <summary>A bounded input override; ineligible states cancel it rather than queue it.</summary>
public sealed class ForcedWalkWindow
{
    private bool active;
    private double until;
    private float direction;

    public void Begin(double now, float directionDegrees)
    { active = true; until = now + ShockImpulse.WalkSeconds; direction = directionDegrees; }

    public bool TryGet(double now, bool eligible, out float directionDegrees)
    {
        directionDegrees = direction;
        if (!active) return false;
        if (!eligible || now >= until) { Clear(); return false; }
        return true;
    }

    public void Clear() { active = false; until = 0; direction = 0; }
}
