using System;
using Desfibrilator.Core;

var actor = new Attempt(7, 100, 900);
var otherTool = new Attempt(8, 101, 901);
var passed = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
void Charge(ReviveCoordinator c, Attempt a, double start = 0)
{
    for (var i = 0; i <= 50; i++) Check(c.Pulse(a, start + i / 10d), "Heartbeat rejected before completion");
}
Run("Five full seconds required despite repeated start messages", () => {
    var c = new ReviveCoordinator();
    for (var i = 0; i <= 49; i++) c.Pulse(actor, i / 10d);
    Check(c.Tick(4.999, _ => true) == null, "Completed early");
    c.Pulse(actor, 5);
    Check(c.Tick(5, _ => true)?.Equals(actor) == true, "Never completed");
});
Run("Global cooldown blocks different tools, users and corpses", () => {
    var c = new ReviveCoordinator(); Charge(c, actor); c.Complete(5);
    Check(!c.Pulse(otherTool, 5) && !c.Pulse(actor, 94.999), "Global cooldown bypassed");
    Check(c.Pulse(otherTool, 95), "Did not become available at 90 seconds");
});
Run("Only one simultaneous charge can acquire the server slot", () => {
    var c = new ReviveCoordinator(); Check(c.Pulse(actor, 0), "First charge rejected");
    Check(!c.Pulse(otherTool, 0), "Second simultaneous charge accepted");
});
Run("Releasing the button requires a fresh five-second charge", () => {
    var c = new ReviveCoordinator(); c.Pulse(actor, 0); c.Cancel(actor.Actor);
    Check(c.CooldownRemaining(1) == 0, "Cancellation consumed cooldown");
    Check(c.Pulse(actor, 1) && c.ChargeRemaining(1) == 5, "Charge progress preserved");
});
Run("Death, distance, invalid corpse or round end cancels immediately", () => {
    foreach (var reason in new[] { "death", "range", "corpse", "round" }) {
        var c = new ReviveCoordinator(); c.Pulse(actor, 0);
        Check(c.Tick(.1, _ => false) == null && !c.Active.HasValue, "Invalid state survived: " + reason);
        Check(c.CooldownRemaining(.1) == 0, "Invalid state consumed cooldown");
    }
});
Run("Interrupted network heartbeats cannot complete a charge", () => {
    var c = new ReviveCoordinator(); c.Pulse(actor, 0);
    Check(c.Tick(.451, _ => true) == null && !c.Active.HasValue, "Timed-out charge survived");
});
Run("Returning heartbeat after interruption restarts rather than resumes", () => {
    var c = new ReviveCoordinator(); c.Pulse(actor, 0); c.Pulse(actor, 2);
    Check(c.ChargeRemaining(2) == 5, "Timed-out charge resumed");
});
Run("Changing item or corpse does not inherit existing progress", () => {
    var c = new ReviveCoordinator(); c.Pulse(actor, 0);
    Check(!c.Pulse(new Attempt(actor.Actor, 102, actor.Corpse), .1), "Changed item inherited progress");
    Check(!c.Pulse(new Attempt(actor.Actor, actor.Tool, 902), .1), "Changed corpse inherited progress");
});
Run("Another player cannot cancel the active player's charge", () => {
    var c = new ReviveCoordinator(); c.Pulse(actor, 0); c.Cancel(otherTool.Actor);
    Check(c.Active.HasValue, "Foreign cancel succeeded");
});
Run("No duplicate completion can restart or shorten the cooldown", () => {
    var c = new ReviveCoordinator(); Charge(c, actor); c.Complete(5);
    var rejected = false; try { c.Complete(6); } catch (InvalidOperationException) { rejected = true; }
    Check(rejected && c.CooldownRemaining(6) == 89, "Duplicate completion changed cooldown");
});
Run("Completing an unfinished charge is rejected", () => {
    var c = new ReviveCoordinator(); c.Pulse(actor, 0);
    var rejected = false; try { c.Complete(.2); } catch (InvalidOperationException) { rejected = true; }
    Check(rejected, "Early completion accepted");
});
Run("Cooldown persists without holding or updating any item", () => {
    var c = new ReviveCoordinator(); Charge(c, actor); c.Complete(5);
    Check(c.CooldownRemaining(50) == 45, "Cooldown depends on item updates");
});
Run("New lobby clears old player IDs, charge and cooldown", () => {
    var c = new ReviveCoordinator(); Charge(c, actor); c.Complete(5); c.ResetSession();
    Check(c.CooldownRemaining(6) == 0 && !c.Active.HasValue && c.Pulse(otherTool, 6), "Session reset failed");
});
Run("Voice buffer ignores leading silence and untransmitted input", () => {
    var b = new LastWordsBuffer(); var hidden = new short[8000]; Array.Fill(hidden, (short)1234);
    b.Append(hidden, hidden.Length, false);
    Check(b.Snapshot().Length == 0, "Silence/closed channel was retained");
});
Run("Only the last utterance survives a silence gap", () => {
    var b = new LastWordsBuffer(); var first = new short[8000]; var last = new short[4000];
    Array.Fill(first, (short)100); Array.Fill(last, (short)200);
    b.Append(first, first.Length, true); b.Append(new short[6400], 6400, false); b.Append(last, last.Length, true);
    var clip = b.Snapshot(); Check(clip.Length == 4000 && Array.TrueForAll(clip, s => s == 200), "Utterances merged");
});
Run("Long speech is bounded to the final five seconds", () => {
    var b = new LastWordsBuffer(); var input = new short[96000];
    for (var i = 0; i < input.Length; i++) input[i] = (short)(i % 20000);
    b.Append(input, input.Length, true); var clip = b.Snapshot();
    Check(clip.Length == 40000, "Unbounded voice buffer");
    for (var i = 0; i < clip.Length; i++) Check(clip[i] == input[input.Length - clip.Length + i], "Ring order corrupted");
});
Run("Trailing silence is trimmed without losing the previous phrase", () => {
    var b = new LastWordsBuffer(); var speech = new short[40000]; Array.Fill(speech, (short)321);
    b.Append(speech, speech.Length, true); b.Append(new short[6400], 6400, false);
    var clip = b.Snapshot(); Check(clip.Length == 40000 && Array.TrueForAll(clip, s => s == 321), "Silence overwrote speech");
});
Run("Too-short noise does not replace a valid last phrase", () => {
    var b = new LastWordsBuffer(); var speech = new short[8000]; Array.Fill(speech, (short)123);
    b.Append(speech, speech.Length, true); b.EndPhrase(); b.Append(new short[100], 100, true);
    Check(b.Snapshot().Length == 8000, "Noise replaced the valid phrase");
});
Run("Voice snapshots are independent copies", () => {
    var b = new LastWordsBuffer(); var speech = new short[8000]; Array.Fill(speech, (short)432);
    b.Append(speech, speech.Length, true); var copy = b.Snapshot(); copy[0] = 0;
    Check(b.Snapshot()[0] == 432, "Caller can mutate retained voice");
});
Run("Orbit or consent revocation removes retained voice", () => {
    var b = new LastWordsBuffer(); b.Append(new short[8000], 8000, true); b.EndPhrase(); b.Clear();
    Check(b.Snapshot().Length == 0, "Phrase survived round/consent clear");
});
Run("Voice and shocks have independent randomized intervals", () => {
    var s = new SideEffectSchedule(); s.Activate(0, () => 0);
    Check(s.NextVoice == 60 && s.NextShock == 45, "Minimum intervals wrong");
    Check(s.Tick(44.99, true, true, () => 0) == SideEffectEvent.None, "Early effect");
    Check(s.Tick(45, true, true, () => 0) == SideEffectEvent.Shock, "Shock not independent");
    Check(s.Tick(60, true, true, () => 0) == SideEffectEvent.Voice, "Voice not independent");
});
Run("Maximum intervals are 120 seconds for voice and 90 for shocks", () => {
    var s = new SideEffectSchedule(); s.Activate(10, () => 1);
    Check(s.NextVoice == 130 && s.NextShock == 100, "Maximum interval wrong");
});
Run("A dead player gets no voice or camera effects", () => {
    var s = new SideEffectSchedule(); s.Activate(0, () => 0);
    Check(s.Tick(200, false, true, () => 0) == SideEffectEvent.None, "Dead player received an effect");
});
Run("Unavailable voice never emits an empty noise event", () => {
    var s = new SideEffectSchedule(); s.Activate(0, () => 0);
    var events = s.Tick(60, true, false, () => 0);
    Check((events & SideEffectEvent.Voice) == 0 && s.NextVoice == 120, "Absent recording emitted noise");
});
Run("Delayed updates do not produce bursts of repeated effects", () => {
    var s = new SideEffectSchedule(); s.Activate(0, () => 0);
    Check(s.Tick(1000, true, true, () => 0) == (SideEffectEvent.Voice | SideEffectEvent.Shock), "Overdue events lost");
    Check(s.Tick(1000, true, true, () => 0) == SideEffectEvent.None, "Duplicate burst");
});
Run("A second revival resets timers without stacking effects", () => {
    var s = new SideEffectSchedule(); s.Activate(0, () => 0); s.Activate(30, () => 0);
    Check(s.Tick(60, true, true, () => 0) == SideEffectEvent.None && s.NextVoice == 90 && s.NextShock == 75, "Effects stacked");
});
Run("Orbit cleanup cancels all future effects", () => {
    var s = new SideEffectSchedule(); s.Activate(0, () => 0); s.Clear();
    Check(!s.Active && s.Tick(200, true, true, () => 0) == SideEffectEvent.None, "Effect survived orbit");
});
Run("Audio upload binds sender, round and death ticket", () => {
    var p = new VoiceUploadPermit(7, 2, 99, 10);
    Check(!p.Allows(8, 2, 99, 10, 20, 1), "Impersonated sender accepted");
    Check(!p.Allows(7, 1, 99, 10, 20, 1), "Previous expedition accepted");
    Check(!p.Allows(7, 2, 98, 10, 20, 1), "Previous death accepted");
    Check(p.TryConsume(7, 2, 99, 10, 20, 1), "Valid owner rejected");
    Check(!p.TryConsume(7, 2, 99, 10, 20, 2), "Duplicate upload accepted");
});
Run("Oversized, negative and truncated audio are rejected before allocation", () => {
    var p = new VoiceUploadPermit(7, 2, 99, 10);
    Check(!p.Allows(7, 2, 99, int.MaxValue, 10, 1), "Huge count accepted");
    Check(!p.Allows(7, 2, 99, -1, 0, 1), "Negative count accepted");
    Check(!p.Allows(7, 2, 99, 40001, 80002, 1), "Long recording accepted");
    Check(!p.Allows(7, 2, 99, 40000, 79999, 1), "Truncated recording accepted");
    Check(p.Allows(7, 2, 99, 40000, 80000, 1), "Valid five-second recording rejected");
});
Run("An expired or revoked permission cannot upload audio", () => {
    var p = new VoiceUploadPermit(7, 2, 99, 10);
    Check(!p.Pending(10.01) && !p.Allows(7, 2, 99, 10, 20, 10.01), "Late upload accepted");
    var q = new VoiceUploadPermit(7, 2, 100, 20); q.Cancel();
    Check(!q.TryConsume(7, 2, 100, 10, 20, 1), "Revoked upload accepted");
});
Run("Declining voice upload is valid and consumes the ticket", () => {
    var p = new VoiceUploadPermit(7, 2, 99, 10);
    Check(p.TryConsume(7, 2, 99, 0, 0, 1) && !p.Pending(1), "Opt-out did not close the request");
});
Run("Five-second PCM recording survives small ordered network chunks", () => {
    var b = new VoiceTransferBuffer(40000);
    var source = new short[40000]; for (var i = 0; i < source.Length; i++) source[i] = (short)(i % 20000);
    for (var offset = 0; offset < source.Length; offset += VoiceTransferBuffer.ChunkSamples)
    {
        var chunk = new short[Math.Min(VoiceTransferBuffer.ChunkSamples, source.Length - offset)];
        Array.Copy(source, offset, chunk, 0, chunk.Length); Check(b.Append(offset, chunk), "Ordered chunk rejected");
    }
    Check(b.Complete, "Transfer never completed"); var result = b.Snapshot();
    for (var i = 0; i < result.Length; i++) Check(result[i] == source[i], "PCM content corrupted");
});
Run("Chunk assembler rejects gaps, duplicates and oversized chunks", () => {
    var b = new VoiceTransferBuffer(1000);
    Check(!b.Append(1, new short[10]), "Gap accepted");
    Check(!b.Append(0, new short[385]), "Oversized packet accepted");
    Check(b.Append(0, new short[384]), "Valid packet rejected");
    Check(!b.Append(0, new short[384]) && b.Position == 384, "Duplicate changed state");
});
Run("Incomplete voice transfers cannot be played", () => {
    var b = new VoiceTransferBuffer(1000); b.Append(0, new short[384]);
    var rejected = false; try { b.Snapshot(); } catch (InvalidOperationException) { rejected = true; }
    Check(rejected && !b.Complete, "Partial audio could be played");
});
Run("Revocation clears partially received samples", () => {
    var b = new VoiceTransferBuffer(200); var chunk = new short[200]; Array.Fill(chunk, (short)99);
    b.Append(0, chunk); b.Clear();
    Check(b.Position == 0 && !b.Complete, "Transfer state survived cleanup");
    b.Append(0, new short[200]); Check(Array.TrueForAll(b.Snapshot(), s => s == 0), "Old samples survived cleanup");
});
Console.WriteLine($"{passed} tests passed; game integration tests are separate and were not run here.");
