using System.Collections.Generic;

namespace Desfibrilator.Core;

public enum RevivalDecision { Apply, AlreadyAlive, Duplicate, WrongPlayer, DifferentDeath }

/// <summary>Authoritative life transitions must not depend on a corpse surviving network despawn.</summary>
public sealed class RevivalReceipts
{
    private readonly Dictionary<ulong, ulong> received = new Dictionary<ulong, ulong>();
    public RevivalDecision Decide(ulong sequence, ulong expectedPlayer, ulong actualPlayer, bool dead,
        ulong expectedCorpse, ulong? actualCorpse)
    {
        if (expectedPlayer != actualPlayer) return RevivalDecision.WrongPlayer;
        if (received.TryGetValue(actualPlayer, out var previous) && sequence <= previous) return RevivalDecision.Duplicate;
        if (!dead) return RevivalDecision.AlreadyAlive;
        if (actualCorpse.HasValue && expectedCorpse != actualCorpse.Value) return RevivalDecision.DifferentDeath;
        return RevivalDecision.Apply;
    }
    public void Commit(ulong sequence, ulong player)
    { if (!received.TryGetValue(player, out var previous) || sequence > previous) received[player] = sequence; }
    public void Clear() => received.Clear();
}
