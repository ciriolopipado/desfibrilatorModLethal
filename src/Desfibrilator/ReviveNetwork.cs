using System;
using System.Collections.Generic;
using System.Linq;
using Desfibrilator.Core;
using GameNetcodeStuff;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Desfibrilator;

public sealed class ReviveNetwork : MonoBehaviour
{
    internal static ReviveNetwork? Instance;
    private const string RequestName = Plugin.Id + ".request.v3", StateName = Plugin.Id + ".state.v3";
    private sealed class SentRevival
    {
        internal ulong Sequence, Client, PlayerObject, Corpse, Tool;
        internal Vector3 Position;
        internal bool Inside, Ship;
        internal double RetryAt, Expires;
        internal readonly HashSet<ulong> Waiting = new HashSet<ulong>();
    }
    private readonly ReviveCoordinator coordinator = new ReviveCoordinator();
    private readonly RevivalReceipts receipts = new RevivalReceipts();
    private readonly Dictionary<ulong, SentRevival> sentRevives = new Dictionary<ulong, SentRevival>();
    private ulong nextRevival;
    private string missingPeersReported = "";
    private readonly HashSet<ulong> peers = new HashSet<ulong>();
    private NetworkManager? manager;
    private bool wasListening, registered;
    private double nextBroadcast, nextHello, cooldownUntil, chargeUntil;
    internal bool Connected { get; private set; }
    internal ulong? ActiveTool { get; private set; }
    internal double Cooldown => Math.Max(0, cooldownUntil - Now);
    internal double ChargeRemaining => Math.Max(0, chargeUntil - Now);
    private static double Now => Time.realtimeSinceStartupAsDouble;

    private void Awake() { Instance = this; }

    private void Update()
    {
        var current = NetworkManager.Singleton;
        if (current != manager)
        {
            Detach(); manager = current;
        }
        if (manager == null || !manager.IsListening)
        {
            if (wasListening) Detach();
            wasListening = false; return;
        }
        wasListening = true;
        if (!registered)
        {
            if (manager.CustomMessagingManager == null) return;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(RequestName, OnRequest);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(StateName, OnState);
            manager.OnClientDisconnectCallback += OnDisconnect;
            registered = true;
        }
        if (manager.IsServer)
        {
            Connected = true; peers.Add(manager.LocalClientId);
            var ready = coordinator.Tick(Now, Validate);
            if (ready.HasValue) Finish(ready.Value);
            RetryUnacknowledgedRevives();
            if (Now >= nextBroadcast) { Broadcast(); nextBroadcast = Now + .15; }
        }
        else if (Now >= nextHello)
        {
            Request(0, 0, 0); nextHello = Now + 2;
        }
    }

    internal void Request(byte action, ulong tool, ulong corpse)
    {
        if (manager == null || !manager.IsListening || (!manager.IsServer && !manager.IsConnectedClient)) return;
        if (manager.IsServer) { Handle(manager.LocalClientId, action, tool, corpse); return; }
        using var writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe(action); writer.WriteValueSafe(tool); writer.WriteValueSafe(corpse);
        manager.CustomMessagingManager.SendNamedMessage(RequestName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
    }

    private void OnRequest(ulong sender, FastBufferReader reader)
    {
        if (manager == null || !manager.IsServer || !reader.TryBeginRead(17)) return;
        reader.ReadValueSafe(out byte action); reader.ReadValueSafe(out ulong tool); reader.ReadValueSafe(out ulong corpse);
        Handle(sender, action, tool, corpse);
    }

    private void Handle(ulong sender, byte action, ulong tool, ulong corpse)
    {
        if (action == 0) { peers.Add(sender); SendSnapshot(sender); return; }
        if (!peers.Contains(sender)) return;
        if (action == 3)
        {
            if (sentRevives.TryGetValue(tool, out var sent) && sent.PlayerObject == corpse && sent.Waiting.Remove(sender))
                Plugin.Log($"Ciriolator revive receipt: event={tool}, observer={sender}, target={sent.Client}.");
            return;
        }
        if (action == 2) { coordinator.Cancel(sender); return; }
        if (action != 1) return;
        var attempt = new Attempt(sender, tool, corpse);
        if (Validate(attempt)) coordinator.Pulse(attempt, Now);
        else coordinator.Cancel(sender);
    }

    private static PlayerControllerB? Player(ulong client) => StartOfRound.Instance?.allPlayerScripts?
        .FirstOrDefault(p => p != null && p.actualClientId == client && (p.isPlayerControlled || p.isPlayerDead));

    private DeadBodyInfo? Body(ulong id)
    {
        if (manager == null || !manager.SpawnManager.SpawnedObjects.TryGetValue(id, out var obj)) return null;
        return obj.GetComponent<RagdollGrabbableObject>()?.ragdoll;
    }

    private bool Validate(Attempt attempt)
    {
        if (manager == null || !Interaction.RoundActive || !manager.ConnectedClients.ContainsKey(attempt.Actor)) return false;
        var missing = string.Join(",", manager.ConnectedClientsIds.Where(id => !peers.Contains(id)).OrderBy(id => id));
        if (missing.Length > 0)
        {
            if (missing != missingPeersReported)
            { missingPeersReported = missing; Plugin.Error($"Ciriolator: revive blocked until every participant loads the same mod version; missing protocol handshake from clients {missing}."); }
            return false;
        }
        missingPeersReported = "";
        var actor = Player(attempt.Actor);
        if (!Interaction.HasActiveTool(actor) || actor!.currentlyHeldObjectServer.NetworkObjectId != attempt.Tool ||
            actor.inSpecialInteractAnimation || actor.inTerminalMenu) return false;
        var body = Body(attempt.Corpse);
        if (!Interaction.IsRecoverable(body)) return false;
        var target = body!.playerScript;
        if (target.thisController == null || target.playerBodyAnimator == null || target.gameplayCamera == null) return false;
        if (!peers.Contains(target.actualClientId) || !manager.ConnectedClients.ContainsKey(target.actualClientId)) return false;
        var position = Interaction.BodyPosition(body);
        var origin = actor.gameplayCamera != null ? actor.gameplayCamera.transform.position : actor.transform.position + Vector3.up;
        if (Vector3.Distance(origin, position) > Interaction.Range + .35f) return false;
        // The client supplies continuous aim/hold heartbeats; the server independently checks geometry and inventory.
        return !Physics.Linecast(origin, position, StartOfRound.Instance.collidersAndRoomMask, QueryTriggerInteraction.Ignore);
    }

    private void Finish(Attempt attempt)
    {
        if (!Validate(attempt)) { coordinator.Cancel(attempt.Actor); return; }
        var body = Body(attempt.Corpse)!;
        var target = body.playerScript;
        var grab = body.grabBodyObject.NetworkObject;
        // Place feet on the walkable floor beside the body; never reuse a position in a wall or over a void.
        if (!Revival.TryFindPosition(body, out var position)) { coordinator.Cancel(attempt.Actor); return; }
        var ship = StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(position);
        // The operator's location is authoritative for bodies carried between factory/exterior.
        var actor = Player(attempt.Actor)!;
        var inside = actor.isInsideFactory;
        try
        {
            Revival.Apply(target, position, inside, ship);
            coordinator.Complete(Now);
            PlayDischarge(attempt.Tool);
            var sent = new SentRevival { Sequence = ++nextRevival, Client = target.actualClientId,
                PlayerObject = target.NetworkObjectId, Corpse = attempt.Corpse, Position = position,
                Inside = inside, Ship = ship, Tool = attempt.Tool, RetryAt = Now + 1, Expires = Now + 5 };
            sentRevives[sent.Sequence] = sent;
            foreach (var peer in peers.Where(p => p != manager!.LocalClientId && manager.ConnectedClients.ContainsKey(p)).ToArray())
            { sent.Waiting.Add(peer); SendRevival(peer, sent); }
            if (grab != null && grab.IsSpawned) grab.Despawn(true);
            Broadcast();
            try { RevivalSideEffects.Instance?.ActivateOnServer(target.actualClientId); }
            catch (Exception effectError) { Plugin.Error($"Player revived, but side effects could not start: {effectError}"); }
            Plugin.Log($"Reanimated client {target.actualClientId}; global cooldown started (90 s).");
        }
        catch (Exception e)
        {
            coordinator.Cancel(attempt.Actor);
            Plugin.Error($"Reanimation failed; inspect host and client logs. {e}");
        }
    }

    private void SendRevival(ulong peer, SentRevival sent)
    {
        using var writer = new FastBufferWriter(96, Allocator.Temp);
        writer.WriteValueSafe((byte)2); writer.WriteValueSafe(sent.Sequence); writer.WriteValueSafe(sent.Client);
        writer.WriteValueSafe(sent.PlayerObject); writer.WriteValueSafe(sent.Corpse); writer.WriteValueSafe(sent.Position);
        writer.WriteValueSafe(sent.Inside); writer.WriteValueSafe(sent.Ship); writer.WriteValueSafe(sent.Tool);
        manager!.CustomMessagingManager.SendNamedMessage(StateName, peer, writer, NetworkDelivery.ReliableSequenced);
    }

    private void RetryUnacknowledgedRevives()
    {
        foreach (var pair in sentRevives.ToArray())
        {
            var sent = pair.Value; var player = Player(sent.Client);
            if (sent.Waiting.Count == 0 || player == null || player.isPlayerDead || player.NetworkObjectId != sent.PlayerObject)
            { sentRevives.Remove(pair.Key); continue; }
            if (Now >= sent.Expires)
            {
                Plugin.Error($"Ciriolator revive event {sent.Sequence}: no confirmation from clients {string.Join(",", sent.Waiting)}. Check their logs and mod versions.");
                sentRevives.Remove(pair.Key); continue;
            }
            if (Now < sent.RetryAt) continue;
            foreach (var peer in sent.Waiting.ToArray())
            {
                if (manager!.ConnectedClients.ContainsKey(peer)) SendRevival(peer, sent);
                else sent.Waiting.Remove(peer);
            }
            sent.RetryAt = Now + 1;
        }
    }

    private void Broadcast()
    {
        cooldownUntil = Now + coordinator.CooldownRemaining(Now);
        chargeUntil = Now + coordinator.ChargeRemaining(Now);
        ActiveTool = coordinator.Active?.Tool;
        foreach (var peer in peers.Where(p => p != manager!.LocalClientId && manager.ConnectedClients.ContainsKey(p)).ToArray()) SendSnapshot(peer);
    }

    private void SendSnapshot(ulong peer)
    {
        if (manager == null || peer == manager.LocalClientId || !manager.ConnectedClients.ContainsKey(peer)) return;
        using var writer = new FastBufferWriter(64, Allocator.Temp);
        writer.WriteValueSafe((byte)1);
        writer.WriteValueSafe(coordinator.CooldownRemaining(Now));
        writer.WriteValueSafe(coordinator.Active.HasValue);
        writer.WriteValueSafe(coordinator.Active?.Tool ?? 0);
        writer.WriteValueSafe(coordinator.ChargeRemaining(Now));
        manager.CustomMessagingManager.SendNamedMessage(StateName, peer, writer, NetworkDelivery.ReliableSequenced);
    }

    private void OnState(ulong sender, FastBufferReader reader)
    {
        if (manager == null || manager.IsServer || sender != NetworkManager.ServerClientId || !reader.TryBeginRead(1)) return;
        reader.ReadValueSafe(out byte kind);
        if (kind == 1 && reader.TryBeginRead(25))
        {
            reader.ReadValueSafe(out double cooldown); reader.ReadValueSafe(out bool active);
            reader.ReadValueSafe(out ulong tool); reader.ReadValueSafe(out double charge);
            cooldownUntil = Now + Math.Max(0, cooldown);
            chargeUntil = Now + Math.Max(0, charge);
            ActiveTool = active ? tool : (ulong?)null; Connected = true;
        }
        else if (kind == 2 && reader.TryBeginRead(54))
        {
            reader.ReadValueSafe(out ulong sequence); reader.ReadValueSafe(out ulong target);
            reader.ReadValueSafe(out ulong playerObject); reader.ReadValueSafe(out ulong corpse);
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out bool inside); reader.ReadValueSafe(out bool ship); reader.ReadValueSafe(out ulong tool);
            var player = Player(target);
            if (player == null) { Plugin.Error($"Ciriolator: missing target {target} for revive event {sequence}; awaiting retry."); return; }
            var currentBody = player.deadBody != null ? player.deadBody.grabBodyObject : null;
            ulong? observedCorpse = currentBody != null && currentBody.IsSpawned ? currentBody.NetworkObjectId : (ulong?)null;
            var decision = receipts.Decide(sequence, playerObject, player.NetworkObjectId, player.isPlayerDead, corpse, observedCorpse);
            if (decision == RevivalDecision.WrongPlayer || decision == RevivalDecision.DifferentDeath)
            { Plugin.Error($"Ciriolator: rejected stale revive event {sequence} for {target}: {decision}."); return; }
            if (decision == RevivalDecision.Apply)
            {
                try { Revival.Apply(player, position, inside, ship); }
                catch (Exception e) { Plugin.Error($"Could not apply host reanimation: {e}"); return; }
            }
            if (decision != RevivalDecision.Duplicate)
            { receipts.Commit(sequence, playerObject); ActiveTool = null; PlayDischarge(tool); }
            Request(3, sequence, playerObject);
        }
    }

    private void PlayDischarge(ulong tool)
    {
        if (manager != null && manager.SpawnManager.SpawnedObjects.TryGetValue(tool, out var obj))
            obj.GetComponent<DefibrillatorItem>()?.PlayDischarge();
    }
    private void OnDisconnect(ulong client) { coordinator.Cancel(client); peers.Remove(client); }
    private void ResetSession()
    {
        coordinator.ResetSession(); peers.Clear(); ActiveTool = null; Connected = false;
        receipts.Clear(); sentRevives.Clear(); nextRevival = 0;
        missingPeersReported = "";
        cooldownUntil = chargeUntil = nextBroadcast = nextHello = 0;
    }
    private void Detach()
    {
        if (manager != null && registered)
        {
            manager.CustomMessagingManager?.UnregisterNamedMessageHandler(RequestName);
            manager.CustomMessagingManager?.UnregisterNamedMessageHandler(StateName);
            manager.OnClientDisconnectCallback -= OnDisconnect;
        }
        registered = false; ResetSession(); wasListening = false;
    }
    private void OnDestroy() { Detach(); if (Instance == this) Instance = null; }
}
