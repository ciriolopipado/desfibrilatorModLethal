using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Desfibrilator.Core;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Desfibrilator;

/// <summary>Server-scheduled side effects. Audio is opt-in, bounded, owner-only on upload and memory-only.</summary>
public sealed class RevivalSideEffects : MonoBehaviour
{
    internal static RevivalSideEffects? Instance;
    private const string RequestName = Plugin.Id + ".effects.request.v1", EventName = Plugin.Id + ".effects.event.v1";
    private sealed class PlayerState
    {
        internal bool Dead;
        internal VoiceUploadPermit? Permit;
        internal VoiceTransferBuffer? Incoming;
        internal double RetryAt, NoiseUntil, NextNoise;
        internal short[] Samples = Array.Empty<short>();
        internal readonly SideEffectSchedule Schedule = new SideEffectSchedule();
    }
    private sealed class Playback
    {
        internal AudioClip Clip = null!;
        internal AudioSource? Source;
    }
    private sealed class Outbound
    {
        internal ulong Peer, Owner;
        internal uint Epoch, Ticket;
        internal bool Upload;
        internal int Position;
        internal short[] Samples = Array.Empty<short>();
    }

    private readonly LastWordsCapture capture = new LastWordsCapture();
    private readonly Dictionary<ulong, PlayerState> states = new Dictionary<ulong, PlayerState>();
    private readonly Dictionary<ulong, Playback> voices = new Dictionary<ulong, Playback>();
    private readonly HashSet<ulong> affected = new HashSet<ulong>(), peers = new HashSet<ulong>();
    private readonly List<Outbound> outgoing = new List<Outbound>();
    private readonly Dictionary<ulong, VoiceTransferBuffer> incoming = new Dictionary<ulong, VoiceTransferBuffer>();
    private readonly Dictionary<ulong, uint> incomingTickets = new Dictionary<ulong, uint>();
    private readonly System.Random random = new System.Random();
    private NetworkManager? manager;
    private ConfigEntry<bool> consent = null!;
    private ConfigEntry<float> cameraStrength = null!;
    private InputAction toggle = null!;
    private bool registered, listening, ready, missionSeen, lastConsent, localWasDead, welcomed;
    private uint epoch = 1, nextTicket, localTicket;
    private double nextHello, localTicketExpires, noticeUntil;
    private string notice = "";
    private static double Now => Time.realtimeSinceStartupAsDouble;
    private static bool AtOrbit => StartOfRound.Instance != null && StartOfRound.Instance.inShipPhase &&
        !StartOfRound.Instance.shipIsLeaving;
    private static PlayerControllerB? Local => StartOfRound.Instance?.localPlayerController;
    private static PlayerControllerB? Player(ulong id) => StartOfRound.Instance?.allPlayerScripts?
        .FirstOrDefault(p => p != null && p.actualClientId == id && (p.isPlayerControlled || p.isPlayerDead));

    private void Awake()
    {
        Instance = this;
        consent = Plugin.Instance.Config.Bind("LastWords", "RecordOwnVoice", false,
            "Opt in to memory-only capture of up to 5 seconds of YOUR transmitted voice. Shared with current teammates for replay after revival. Cleared at orbit/disconnect. F8 toggles by default.");
        cameraStrength = Plugin.Instance.Config.Bind("SideEffects", "CameraStrength", 1f,
            new ConfigDescription("Camera impulse multiplier. 0 disables camera movement locally; no damage. Defaults: random yaw 4–10°, pitch 2–6°.", new AcceptableValueRange<float>(0, 1)));
        var binding = Plugin.Instance.Config.Bind("Controls", "ToggleLastWordsRecording", "<Keyboard>/f8", "Toggle your own voice recording consent. Restart after rebinding.");
        toggle = new InputAction("Ciriolator last words consent", InputActionType.Button, binding.Value);
        toggle.Enable(); lastConsent = consent.Value;
    }

    private void Update()
    {
        BindManager();
        if (!registered || manager == null || !manager.IsListening) { capture.Update(false, false, false); return; }
        if (!manager.IsServer && !manager.IsConnectedClient) { capture.Update(false, false, false); return; }
        var local = Local;
        if (local != null && !welcomed)
        {
            Show(consent.Value ? "Ciriolator: últimas palabras activadas (solo memoria). F8 para desactivar."
                : "Ciriolator: F8 activa tus últimas palabras (solo memoria, voz de partida). Grabación desactivada.");
            welcomed = true;
        }
        if (local != null && !local.isTypingChat && !local.inTerminalMenu && toggle.WasPressedThisFrame()) consent.Value = !consent.Value;
        if (consent.Value != lastConsent)
        {
            lastConsent = consent.Value;
            Show(lastConsent ? "Ciriolator: grabación de tu voz activada (memoria hasta órbita)." : "Ciriolator: grabación desactivada; se elimina tu frase.");
            if (!lastConsent)
            {
                capture.Dispose();
                if (local != null) { CancelTransfers(local.actualClientId); ClearVoice(local.actualClientId); }
                SendRevoke();
            }
        }
        if (Interaction.RoundActive) missionSeen = true;
        if (missionSeen && AtOrbit)
        {
            missionSeen = false;
            if (manager.IsServer) { epoch++; ClearRound(); SendAll(0); }
            else ClearLocal();
        }

        var alive = local != null && local.isPlayerControlled && !local.isPlayerDead;
        var playingOwnEcho = local != null && voices.TryGetValue(local.actualClientId, out var own) && own.Source != null && own.Source.isPlaying;
        capture.Update(consent.Value, alive && Interaction.RoundActive, playingOwnEcho);
        if (local != null && local.isPlayerDead && !localWasDead) capture.FreezeAtDeath();
        localWasDead = local != null && local.isPlayerDead;
        if (manager.IsServer) { ready = true; peers.Add(manager.LocalClientId); ServerTick(); }
        else if (!ready && local != null && Now >= nextHello) { SendHello(); nextHello = Now + 1; }
        if (localTicket != 0 && local != null && local.isPlayerDead && Now <= localTicketExpires)
        {
            var ticket = localTicket; localTicket = 0;
            Upload(ticket, consent.Value ? capture.Frozen : Array.Empty<short>());
        }
        if (Now > localTicketExpires) localTicket = 0;
        PumpTransfers();
        foreach (var pair in voices)
        {
            var player = Player(pair.Key);
            if (pair.Value.Source != null && (player == null || player.isPlayerDead || !Interaction.RoundActive)) pair.Value.Source.Stop();
        }
    }

    private void BindManager()
    {
        var current = NetworkManager.Singleton;
        if (manager != current) { Detach(); manager = current; }
        if (manager == null || !manager.IsListening) { if (listening) Detach(); return; }
        listening = true;
        if (registered || manager.CustomMessagingManager == null) return;
        manager.CustomMessagingManager.RegisterNamedMessageHandler(RequestName, OnRequest);
        manager.CustomMessagingManager.RegisterNamedMessageHandler(EventName, OnEvent);
        manager.OnClientDisconnectCallback += OnDisconnect;
        registered = true;
    }

    internal void ActivateOnServer(ulong client)
    {
        if (manager == null || !manager.IsServer || !Interaction.RoundActive) return;
        if (!states.TryGetValue(client, out var state)) states[client] = state = new PlayerState();
        state.Dead = false;
        state.Schedule.Activate(Now, random.NextDouble);
        affected.Add(client);
        SendAll(3, client);
        if (state.Samples.Length > 0) RelayClip(client, state.Samples);
    }

    private void ServerTick()
    {
        if (manager == null || StartOfRound.Instance == null) return;
        foreach (var client in manager.ConnectedClientsIds.ToArray())
        {
            var player = Player(client);
            if (player == null) continue;
            if (!states.TryGetValue(client, out var state)) states[client] = state = new PlayerState { Dead = false };
            if (Interaction.RoundActive && player.isPlayerDead && !state.Dead)
            {
                state.Dead = true; state.NoiseUntil = 0;
                CancelTransfers(client); ClearSamples(state); ClearVoice(client); SendAll(6, client);
                state.Permit = new VoiceUploadPermit(client, epoch, ++nextTicket, Now + 10); state.RetryAt = 0;
            }
            if (!player.isPlayerDead) state.Dead = false;
            if (state.Permit?.Pending(Now) == true && Now >= state.RetryAt)
            {
                if (client == manager.LocalClientId) QueueTicket(state.Permit.Ticket);
                else if (peers.Contains(client)) SendTicket(client, state.Permit.Ticket);
                state.RetryAt = Now + 1;
            }
            if (state.Permit?.Pending(Now) == false && state.Incoming != null)
            {
                state.Incoming.Clear(); state.Incoming = null;
                Plugin.Log($"Ciriolator: incomplete voice transfer discarded for client {client}.");
            }
            if (!Interaction.RoundActive) continue;
            var events = state.Schedule.Tick(Now, !player.isPlayerDead, state.Samples.Length > 0, random.NextDouble);
            if ((events & SideEffectEvent.Voice) != 0)
            {
                PlayEcho(client); SendAll(4, client);
                state.NoiseUntil = Now + (double)state.Samples.Length / LastWordsBuffer.SampleRate;
                state.NextNoise = Now;
            }
            if ((events & SideEffectEvent.Shock) != 0)
            {
                var yaw = Signed(4, 10); var pitch = Signed(2, 6);
                Shock(client, yaw, pitch); SendAll(5, client, yaw, pitch);
            }
            if (!player.isPlayerDead && Now < state.NoiseUntil && Now >= state.NextNoise)
            {
                // Use the game's noise system; only enemies that react to sound are attracted.
                try
                {
                    RoundManager.Instance?.PlayAudibleNoise(player.transform.position, 12, .7f, 0,
                        player.isInHangarShipRoom && StartOfRound.Instance.hangarDoorsClosed, 0);
                }
                catch (Exception error) { Plugin.Error($"An enemy-noise integration failed: {error}"); }
                state.NextNoise = Now + 1;
            }
        }
    }

    private float Signed(float min, float max) => (float)(min + (max - min) * random.NextDouble()) * (random.Next(2) == 0 ? -1 : 1);

    private void SendHello()
    {
        using var writer = new FastBufferWriter(8, Allocator.Temp);
        writer.WriteValueSafe((byte)0); writer.WriteValueSafe(epoch);
        SendRequest(writer);
    }
    private void SendRevoke()
    {
        if (manager == null || !registered) return;
        if (manager.IsServer) { Revoke(manager.LocalClientId); return; }
        using var writer = new FastBufferWriter(8, Allocator.Temp);
        writer.WriteValueSafe((byte)2); writer.WriteValueSafe(epoch); SendRequest(writer);
    }
    private void SendRequest(FastBufferWriter writer) => manager!.CustomMessagingManager.SendNamedMessage(RequestName,
        NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);

    private void OnRequest(ulong sender, FastBufferReader reader)
    {
        if (manager == null || !manager.IsServer || !manager.ConnectedClients.ContainsKey(sender) || !reader.TryBeginRead(5)) return;
        reader.ReadValueSafe(out byte kind); reader.ReadValueSafe(out uint messageEpoch);
        if (kind == 0)
        {
            var first = peers.Add(sender); SendOne(sender, 0);
            if (first)
                foreach (var pair in states.Where(p => p.Value.Schedule.Active).ToArray())
                {
                    SendOne(sender, 3, pair.Key);
                    if (pair.Value.Samples.Length > 0) SendClip(sender, pair.Key, pair.Value.Samples, pair.Value.Permit?.Ticket ?? 0);
                }
            return;
        }
        if (messageEpoch != epoch || !peers.Contains(sender)) return;
        if (kind == 2) { Revoke(sender); return; }
        if (kind != 1 || !reader.TryBeginRead(16)) return;
        reader.ReadValueSafe(out uint ticket); reader.ReadValueSafe(out int count);
        reader.ReadValueSafe(out int offset); reader.ReadValueSafe(out int chunkCount);
        if (!states.TryGetValue(sender, out var state) ||
            state.Permit?.Allows(sender, epoch, ticket, count, count <= LastWordsBuffer.MaxSamples && count >= 0 ? count * 2 : -1, Now) != true ||
            chunkCount < 0 || chunkCount > VoiceTransferBuffer.ChunkSamples || reader.Length - reader.Position != chunkCount * 2) return;
        if (count == 0) { if (offset == 0 && chunkCount == 0) AcceptUpload(sender, ticket, Array.Empty<short>()); return; }
        if (chunkCount == 0) return;
        if (state.Incoming == null)
        {
            if (offset != 0) return;
            state.Incoming = new VoiceTransferBuffer(count);
        }
        if (state.Incoming.Count != count) return;
        var bytes = new byte[chunkCount * 2];
        reader.ReadBytesSafe(ref bytes, bytes.Length);
        var samples = new short[chunkCount]; Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
        if (state.Incoming.Append(offset, samples) && state.Incoming.Complete)
        {
            var complete = state.Incoming.Snapshot(); AcceptUpload(sender, ticket, complete);
            Array.Clear(complete, 0, complete.Length);
        }
        Array.Clear(bytes, 0, bytes.Length); Array.Clear(samples, 0, samples.Length);
    }

    private void AcceptUpload(ulong sender, uint ticket, short[] samples)
    {
        if (!states.TryGetValue(sender, out var state) || state.Permit?.TryConsume(sender, epoch, ticket, samples.Length, samples.Length * 2, Now) != true) return;
        state.Incoming?.Clear(); state.Incoming = null;
        ClearSamples(state); state.Samples = (short[])samples.Clone();
        if (state.Schedule.Active && samples.Length > 0 && !state.Dead) RelayClip(sender, samples);
    }

    private void Upload(uint ticket, short[] samples)
    {
        if (manager == null) return;
        if (manager.IsServer) { AcceptUpload(manager.LocalClientId, ticket, samples); return; }
        foreach (var task in outgoing.Where(t => t.Upload)) Array.Clear(task.Samples, 0, task.Samples.Length);
        outgoing.RemoveAll(t => t.Upload);
        outgoing.Add(new Outbound { Peer = NetworkManager.ServerClientId, Owner = manager.LocalClientId,
            Epoch = epoch, Ticket = ticket, Upload = true, Samples = (short[])samples.Clone() });
    }

    private void QueueTicket(uint ticket) { localTicket = ticket; localTicketExpires = Now + 10; }
    private void SendTicket(ulong peer, uint ticket)
    {
        using var writer = Header(1); writer.WriteValueSafe(ticket); SendEvent(peer, writer);
    }
    private void Revoke(ulong client)
    {
        CancelTransfers(client);
        if (states.TryGetValue(client, out var state)) { ClearSamples(state); state.Permit?.Cancel(); state.NoiseUntil = 0; }
        ClearVoice(client); SendAll(6, client);
    }
    private FastBufferWriter Header(byte kind, int capacity = 32)
    {
        var writer = new FastBufferWriter(capacity, Allocator.Temp);
        writer.WriteValueSafe(kind); writer.WriteValueSafe(epoch); return writer;
    }
    private void SendEvent(ulong peer, FastBufferWriter writer)
    {
        if (manager != null && peer != manager.LocalClientId && manager.ConnectedClients.ContainsKey(peer))
            manager.CustomMessagingManager.SendNamedMessage(EventName, peer, writer, NetworkDelivery.ReliableSequenced);
    }
    private void SendOne(ulong peer, byte kind, ulong client = 0, float yaw = 0, float pitch = 0)
    {
        using var writer = Header(kind);
        if (kind != 0) writer.WriteValueSafe(client);
        if (kind == 5) { writer.WriteValueSafe(yaw); writer.WriteValueSafe(pitch); }
        SendEvent(peer, writer);
    }
    private void SendAll(byte kind, ulong client = 0, float yaw = 0, float pitch = 0)
    {
        foreach (var peer in peers.ToArray()) SendOne(peer, kind, client, yaw, pitch);
    }
    private void SendClip(ulong peer, ulong client, short[] samples, uint ticket)
    {
        if (manager == null || peer == manager.LocalClientId) return;
        outgoing.RemoveAll(t => !t.Upload && t.Peer == peer && t.Owner == client);
        // Share this bounded server cache between recipients; don't allocate a full copy per peer.
        outgoing.Add(new Outbound { Peer = peer, Owner = client, Epoch = epoch, Ticket = ticket, Samples = samples });
    }
    private void RelayClip(ulong client, short[] samples)
    {
        InstallClip(client, samples);
        if (states.TryGetValue(client, out var state))
            foreach (var peer in peers.ToArray()) SendClip(peer, client, state.Samples, state.Permit?.Ticket ?? 0);
    }

    private void PumpTransfers()
    {
        // Keep every audio message below 1 KiB and pace traffic rather than overflowing a reliable send window.
        for (var sent = 0; sent < 4 && outgoing.Count > 0; sent++)
        {
            var task = outgoing[0]; outgoing.RemoveAt(0);
            if (manager == null || task.Epoch != epoch || (!task.Upload && !manager.ConnectedClients.ContainsKey(task.Peer))) continue;
            var count = Math.Min(VoiceTransferBuffer.ChunkSamples, task.Samples.Length - task.Position);
            var bytes = new byte[count * 2]; Buffer.BlockCopy(task.Samples, task.Position * 2, bytes, 0, bytes.Length);
            using var writer = Header(task.Upload ? (byte)1 : (byte)2, 1024);
            if (!task.Upload) writer.WriteValueSafe(task.Owner);
            writer.WriteValueSafe(task.Ticket); writer.WriteValueSafe(task.Samples.Length);
            writer.WriteValueSafe(task.Position); writer.WriteValueSafe(count); writer.WriteBytesSafe(bytes);
            if (task.Upload) SendRequest(writer); else SendEvent(task.Peer, writer);
            task.Position += count; Array.Clear(bytes, 0, bytes.Length);
            if (task.Position < task.Samples.Length) outgoing.Add(task);
            else if (task.Upload) Array.Clear(task.Samples, 0, task.Samples.Length);
        }
    }

    private void CancelTransfers(ulong owner)
    {
        foreach (var task in outgoing.Where(t => t.Owner == owner && t.Upload)) Array.Clear(task.Samples, 0, task.Samples.Length);
        outgoing.RemoveAll(t => t.Owner == owner);
    }

    private void OnEvent(ulong sender, FastBufferReader reader)
    {
        if (manager == null || manager.IsServer || sender != NetworkManager.ServerClientId || !reader.TryBeginRead(5)) return;
        reader.ReadValueSafe(out byte kind); reader.ReadValueSafe(out uint messageEpoch);
        if (kind == 0)
        {
            if (epoch != messageEpoch) { ClearLocal(); epoch = messageEpoch; }
            ready = true; return;
        }
        if (!ready || messageEpoch != epoch) return;
        if (kind == 1 && reader.TryBeginRead(4)) { reader.ReadValueSafe(out uint ticket); QueueTicket(ticket); return; }
        if (!reader.TryBeginRead(8)) return;
        reader.ReadValueSafe(out ulong client);
        if (kind == 2 && reader.TryBeginRead(16) && affected.Contains(client))
        {
            reader.ReadValueSafe(out uint ticket); reader.ReadValueSafe(out int count);
            reader.ReadValueSafe(out int offset); reader.ReadValueSafe(out int chunkCount);
            if (count <= 0 || count > LastWordsBuffer.MaxSamples || chunkCount <= 0 || chunkCount > VoiceTransferBuffer.ChunkSamples ||
                reader.Length - reader.Position != chunkCount * 2) return;
            if (!incoming.TryGetValue(client, out var assembly))
            {
                if (offset != 0) return;
                incoming[client] = assembly = new VoiceTransferBuffer(count); incomingTickets[client] = ticket;
            }
            if (assembly.Count != count || incomingTickets[client] != ticket) return;
            var bytes = new byte[chunkCount * 2]; reader.ReadBytesSafe(ref bytes, bytes.Length);
            var samples = new short[chunkCount]; Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
            if (assembly.Append(offset, samples) && assembly.Complete)
            {
                var complete = assembly.Snapshot(); InstallClip(client, complete); Array.Clear(complete, 0, complete.Length);
            }
            Array.Clear(bytes, 0, bytes.Length); Array.Clear(samples, 0, samples.Length);
        }
        else if (kind == 3) affected.Add(client);
        else if (kind == 4 && affected.Contains(client)) PlayEcho(client);
        else if (kind == 5 && affected.Contains(client) && reader.TryBeginRead(8))
        {
            reader.ReadValueSafe(out float yaw); reader.ReadValueSafe(out float pitch);
            if (!float.IsNaN(yaw) && !float.IsInfinity(yaw) && !float.IsNaN(pitch) && !float.IsInfinity(pitch))
                Shock(client, Mathf.Clamp(yaw, -10, 10), Mathf.Clamp(pitch, -6, 6));
        }
        else if (kind == 6) ClearVoice(client);
        else if (kind == 7) { affected.Remove(client); ClearVoice(client); }
    }

    private void InstallClip(ulong client, short[] samples)
    {
        ClearVoice(client);
        if (samples.Length == 0) return;
        var data = new float[samples.Length];
        for (var i = 0; i < samples.Length; i++) data[i] = samples[i] / 32768f;
        var clip = AudioClip.Create("CiriolatorLastWords", data.Length, 1, LastWordsBuffer.SampleRate, false);
        clip.SetData(data, 0); Array.Clear(data, 0, data.Length);
        voices[client] = new Playback { Clip = clip };
    }
    private void PlayEcho(ulong client)
    {
        var player = Player(client);
        if (player == null || player.isPlayerDead || !Interaction.RoundActive || !voices.TryGetValue(client, out var voice) ||
            (!player.IsOwner && player.voicePlayerState?.IsLocallyMuted == true)) return;
        if (voice.Source == null)
        {
            var obj = new GameObject("CiriolatorProximityEcho"); obj.transform.SetParent(player.transform, false);
            obj.transform.localPosition = Vector3.up * 1.5f;
            voice.Source = obj.AddComponent<AudioSource>();
            voice.Source.playOnAwake = false; voice.Source.loop = false;
            voice.Source.spatialBlend = 1; voice.Source.minDistance = 1; voice.Source.maxDistance = 12;
            voice.Source.rolloffMode = AudioRolloffMode.Linear; voice.Source.volume = .7f;
            voice.Source.pitch = .98f;
            if (player.currentVoiceChatAudioSource != null) voice.Source.outputAudioMixerGroup = player.currentVoiceChatAudioSource.outputAudioMixerGroup;
            obj.AddComponent<AudioLowPassFilter>().cutoffFrequency = 2800;
        }
        // Independent proximity source: never transmit through the game's walkie-talkie system.
        voice.Source.clip = voice.Clip; voice.Source.Play();
    }
    private void Shock(ulong client, float yaw, float pitch)
    {
        var player = Player(client);
        if (player == null || player.isPlayerDead || !Interaction.RoundActive) return;
        if (player.itemAudio != null) player.itemAudio.PlayOneShot(ItemFactory.Discharge, .35f);
        if (!player.IsOwner || player.disableLookInput || player.inTerminalMenu || player.inSpecialInteractAnimation) return;
        var amount = cameraStrength.Value;
        if (amount <= 0) return;
        player.transform.Rotate(0, yaw * amount, 0, Space.World);
        var cameraUp = AccessTools.Field(typeof(PlayerControllerB), "cameraUp");
        if (cameraUp != null && player.gameplayCamera != null)
        {
            var value = Mathf.Clamp((float)cameraUp.GetValue(player) + pitch * amount, -80, 80);
            cameraUp.SetValue(player, value);
            var euler = player.gameplayCamera.transform.localEulerAngles;
            player.gameplayCamera.transform.localEulerAngles = new Vector3(value, euler.y, euler.z);
        }
        // No recenter coroutine: the resulting direction remains until the player moves the view.
    }

    private void ClearVoice(ulong client)
    {
        if (incoming.TryGetValue(client, out var assembly)) { assembly.Clear(); incoming.Remove(client); }
        incomingTickets.Remove(client);
        if (!voices.TryGetValue(client, out var voice)) return;
        if (voice.Source != null) { voice.Source.Stop(); Destroy(voice.Source.gameObject); }
        if (voice.Clip != null) Destroy(voice.Clip);
        voices.Remove(client);
    }
    private static void ClearSamples(PlayerState state)
    {
        state.Incoming?.Clear(); state.Incoming = null;
        Array.Clear(state.Samples, 0, state.Samples.Length); state.Samples = Array.Empty<short>();
    }
    private void ClearLocal()
    {
        foreach (var client in voices.Keys.ToArray()) ClearVoice(client);
        foreach (var assembly in incoming.Values) assembly.Clear(); incoming.Clear(); incomingTickets.Clear();
        foreach (var task in outgoing.Where(t => t.Upload)) Array.Clear(task.Samples, 0, task.Samples.Length);
        outgoing.Clear();
        affected.Clear(); capture.ClearRound(); localTicket = 0; localWasDead = false;
    }
    private void ClearRound()
    {
        ClearLocal(); foreach (var state in states.Values) ClearSamples(state); states.Clear();
    }
    private void OnDisconnect(ulong client)
    {
        if (manager?.IsServer == true)
        {
            CancelTransfers(client); outgoing.RemoveAll(t => t.Peer == client);
            if (states.TryGetValue(client, out var state)) ClearSamples(state);
            states.Remove(client); affected.Remove(client); ClearVoice(client); SendAll(7, client); peers.Remove(client);
        }
        if (manager != null && client == manager.LocalClientId) { ClearRound(); capture.Dispose(); }
    }
    private void Detach()
    {
        if (manager != null && registered)
        {
            manager.CustomMessagingManager?.UnregisterNamedMessageHandler(RequestName);
            manager.CustomMessagingManager?.UnregisterNamedMessageHandler(EventName);
            manager.OnClientDisconnectCallback -= OnDisconnect;
        }
        ClearRound(); capture.Dispose(); peers.Clear(); registered = listening = ready = missionSeen = welcomed = false;
        nextHello = 0; epoch = 1;
    }
    private void Show(string text) { notice = text; noticeUntil = Now + 8; Plugin.Log(text); }
    private void OnGUI()
    {
        if (Local == null || Now > noticeUntil) return;
        GUI.Box(new Rect(Screen.width / 2f - 340, Screen.height * .85f, 680, 45), notice);
    }
    private void OnDestroy() { Detach(); toggle?.Dispose(); if (Instance == this) Instance = null; }
}
