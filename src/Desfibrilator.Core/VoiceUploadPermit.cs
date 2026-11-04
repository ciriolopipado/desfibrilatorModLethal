namespace Desfibrilator.Core;

/// <summary>One bounded upload from the owner, for exactly one death and expedition.</summary>
public sealed class VoiceUploadPermit
{
    public ulong Owner { get; }
    public uint Epoch { get; }
    public uint Ticket { get; }
    private readonly double expires;
    private bool consumed;
    public VoiceUploadPermit(ulong owner, uint epoch, uint ticket, double expires)
    { Owner = owner; Epoch = epoch; Ticket = ticket; this.expires = expires; }
    public bool Pending(double now) => !consumed && now <= expires;
    public bool Allows(ulong owner, uint epoch, uint ticket, int count, int bytes, double now) =>
        Pending(now) && Owner == owner && Epoch == epoch && Ticket == ticket &&
        count >= 0 && count <= LastWordsBuffer.MaxSamples && bytes == count * 2;
    public bool TryConsume(ulong owner, uint epoch, uint ticket, int count, int bytes, double now)
    {
        if (!Allows(owner, epoch, ticket, count, bytes, now)) return false;
        consumed = true; return true;
    }
    public void Cancel() => consumed = true;
}
