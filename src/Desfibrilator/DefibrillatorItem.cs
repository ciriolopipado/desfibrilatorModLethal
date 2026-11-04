using UnityEngine;
using GameNetcodeStuff;

namespace Desfibrilator;

public sealed class DefibrillatorItem : GrabbableObject
{
    public Transform Visual = null!, LeftPaddle = null!, RightPaddle = null!;
    public AudioSource Sound = null!;
    private bool charging;
    private double nextPulse;
    private ulong? requestedBody;

    public override void Update()
    {
        base.Update();
        var net = ReviveNetwork.Instance;
        if (net == null || !IsSpawned) return;
        var player = playerHeldBy;
        var isLocal = player != null && player.IsOwner && Interaction.HasActiveTool(player) && player.currentlyHeldObjectServer == this;
        var body = isLocal ? Interaction.FindAimedBody(player!) : null;
        var wantsCharge = isLocal && body != null && Interaction.IsRecoverable(body) &&
            Interaction.RoundActive && !player!.isTypingChat && !player.inTerminalMenu &&
            !player.inSpecialInteractAnimation && Plugin.Use.IsPressed() && net.Cooldown <= 0;
        if (wantsCharge)
        {
            var id = body!.grabBodyObject.NetworkObjectId;
            if (requestedBody != id) { CancelRequest(); requestedBody = id; nextPulse = 0; }
            if (Time.realtimeSinceStartupAsDouble >= nextPulse)
            {
                net.Request(1, NetworkObjectId, id);
                nextPulse = Time.realtimeSinceStartupAsDouble + .1;
            }
        }
        else CancelRequest();

        var active = net.ActiveTool == NetworkObjectId;
        if (active && !charging) { Sound.clip = ItemFactory.Charge; Sound.loop = false; Sound.Play(); }
        if (!active && charging) Sound.Stop();
        charging = active;
    }

    public override void LateUpdate()
    {
        base.LateUpdate();
        var active = ReviveNetwork.Instance != null && ReviveNetwork.Instance.ActiveTool == NetworkObjectId;
        // Move both paddles into a treatment pose. Uses the game's existing two-hand holding pose.
        var amount = active ? Mathf.Clamp01((float)(5 - ReviveNetwork.Instance!.ChargeRemaining) / .6f) : 0;
        LeftPaddle.localPosition = Vector3.Lerp(new Vector3(-.21f, 0, 0), new Vector3(-.13f, .04f, .20f), amount);
        RightPaddle.localPosition = Vector3.Lerp(new Vector3(.21f, 0, 0), new Vector3(.13f, .04f, .20f), amount);
        Visual.localRotation = Quaternion.Euler(amount * 20, 0, 0);
    }

    internal void PlayDischarge() { charging = false; Sound.Stop(); Sound.PlayOneShot(ItemFactory.Discharge); }
    private void CancelRequest()
    {
        if (!requestedBody.HasValue) return;
        ReviveNetwork.Instance?.Request(2, NetworkObjectId, requestedBody.Value);
        requestedBody = null;
    }
    public override void PocketItem() { CancelRequest(); base.PocketItem(); }
    public override void DiscardItem() { CancelRequest(); base.DiscardItem(); }
    private void OnDisable() { CancelRequest(); if (Sound != null) Sound.Stop(); charging = false; }

    private void OnGUI()
    {
        if (playerHeldBy == null || !playerHeldBy.IsOwner || !Interaction.HasActiveTool(playerHeldBy) ||
            playerHeldBy.currentlyHeldObjectServer != this) return;
        var net = ReviveNetwork.Instance;
        if (net == null) return;
        string text;
        if (!net.Connected) text = "Ciriolator: conectando con el anfitrión…";
        else if (net.Cooldown > 0)
        {
            var seconds = Mathf.CeilToInt((float)net.Cooldown);
            text = $"Ciriolator no disponible: {seconds / 60:00}:{seconds % 60:00}";
        }
        else if (net.ActiveTool == NetworkObjectId) text = $"Desfibrilando… {Mathf.Clamp01((float)(5 - net.ChargeRemaining) / 5) * 100:0}%";
        else if (net.ActiveTool.HasValue) text = "Ciriolator ocupado: reanimación en curso";
        else text = "Ciriolator listo · Mantén E sobre un cadáver recuperable (5 s)";
        GUI.Box(new Rect(Screen.width / 2f - 260, Screen.height * .78f, 520, 40), text);
    }
}
