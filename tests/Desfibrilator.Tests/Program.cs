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
Console.WriteLine($"{passed} tests passed; game integration tests are separate and were not run here.");
