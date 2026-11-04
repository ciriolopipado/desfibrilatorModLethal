using System;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace Desfibrilator;

internal static class Revival
{
    internal static bool TryFindPosition(DeadBodyInfo body, out Vector3 position)
    {
        var center = Interaction.BodyPosition(body);
        var offsets = new[] { Vector3.zero, Vector3.right * .6f, Vector3.left * .6f, Vector3.forward * .6f, Vector3.back * .6f };
        var mask = StartOfRound.Instance.collidersAndRoomMask;
        foreach (var offset in offsets)
        {
            var origin = center + offset + Vector3.up * .7f;
            if (!Physics.Raycast(origin, Vector3.down, out var floor, 2.5f, mask, QueryTriggerInteraction.Ignore)) continue;
            var feet = floor.point + Vector3.up * .08f;
            if (Physics.CheckCapsule(feet + Vector3.up * .35f, feet + Vector3.up * 1.5f, .28f, mask, QueryTriggerInteraction.Ignore)) continue;
            // The candidate must remain beside the corpse, on the same side of walls.
            if (Physics.Linecast(center + Vector3.up * .25f, feet + Vector3.up * .5f, mask, QueryTriggerInteraction.Ignore)) continue;
            position = feet; return true;
        }
        position = default; return false;
    }

    internal static void Apply(PlayerControllerB player, Vector3 position, bool inside, bool ship)
    {
        if (!player.isPlayerDead) return;
        var round = StartOfRound.Instance;
        var body = player.deadBody;
        // Teleport before committing the alive state: a patched/failed teleport must not consume a revival.
        player.TeleportPlayer(position, false, 0, false, true);
        // Do not call ReviveDeadPlayers(): that method revives every dead player and changes round counters.
        player.isPlayerDead = false;
        player.isPlayerControlled = true;
        player.health = 30;
        player.criticallyInjured = false;
        player.hasBeenCriticallyInjured = false;
        player.bleedingHeavily = false;
        player.isClimbingLadder = false;
        player.inSpecialInteractAnimation = false;
        player.inAnimationWithEnemy = null;
        player.activatingItem = false;
        player.disableLookInput = false;
        player.disableMoveInput = false;
        player.disableInteract = false;
        player.isSinking = false;
        player.sinkingValue = 0;
        player.sourcesCausingSinking = 0;
        player.isMovementHindered = 0;
        player.hinderedMultiplier = 1;
        player.isUnderwater = false;
        SetInternal(player, "isFaceUnderwaterOnServer", false);
        SetInternal(player, "wasUnderwaterLastFrame", false);
        player.isExhausted = false;
        player.sprintMeter = 1;
        player.setPositionOfDeadPlayer = false;
        player.isInsideFactory = inside;
        player.isInHangarShipRoom = ship;
        player.isInElevator = ship;
        player.isHoldingObject = false;
        player.twoHanded = player.twoHandedAnimation = false;
        player.currentlyHeldObjectServer = null;
        player.carryWeight = 1;
        player.thisController.enabled = true;
        player.deadBody = null;
        if (body != null) { body.gameObject.SetActive(false); UnityEngine.Object.Destroy(body.gameObject); }
        // Inventory is verified empty before use. Never spawn, destroy, or clear inventory items here.
        round.livingPlayers = round.allPlayerScripts.Count(p => p != null && p.isPlayerControlled && !p.isPlayerDead);
        round.allPlayersDead = round.livingPlayers == 0;
        // Cosmetic/HUD mod failures must not prevent peers receiving the committed alive state.
        Present(() => {
            AccessTools.Method(typeof(PlayerControllerB), "ResetFallGravity").Invoke(player, Array.Empty<object>());
            player.ResetPlayerBloodObjects();
        });
        Present(() => {
            player.playerBodyAnimator.enabled = true;
            player.playerBodyAnimator.SetBool("Limp", false);
            player.playerBodyAnimator.SetBool("dead", false);
            player.playerBodyAnimator.SetBool("HoldLung", false);
            player.DisablePlayerModel(player.gameObject, true, true);
            player.thisPlayerModel.enabled = !player.IsOwner;
            player.thisPlayerModelLOD1.enabled = !player.IsOwner;
            player.thisPlayerModelLOD2.enabled = !player.IsOwner;
        });
        if (player.IsOwner) Present(() =>
        {
            SetInternal(player, "hasBegunSpectating", false);
            player.spectatedPlayerScript = null;
            SetInternal(player, "isCameraDisabled", false);
            player.gameplayCamera.enabled = true;
            player.thisPlayerModelArms.enabled = true;
            round.SetSpectateCameraToGameOverMode(false, player);
            round.SwitchCamera(player.gameplayCamera);
            HUDManager.Instance.HideHUD(false);
            HUDManager.Instance.UpdateHealthUI(30, false);
            HUDManager.Instance.gameOverAnimator.SetTrigger("revive");
            player.statusEffectAudio.Stop();
            if (SoundManager.Instance != null) SoundManager.Instance.SetDiageticMixerSnapshot(0, .1f);
        });
        Present(round.UpdatePlayerVoiceEffects);
    }

    private static void Present(Action action)
    {
        try { action(); }
        catch (Exception e) { Plugin.Error($"Player revived, but a presentation/voice integration failed: {e}"); }
    }

    private static void SetInternal(PlayerControllerB player, string name, bool value)
    {
        // These fields can be private in the shipped game although publicized reference snapshots expose them.
        var field = AccessTools.Field(typeof(PlayerControllerB), name);
        if (field != null) field.SetValue(player, value);
        else Plugin.Error($"Game API mismatch: PlayerControllerB.{name} was not found.");
    }
}
