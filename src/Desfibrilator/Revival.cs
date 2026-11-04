using System;
using System.Collections;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

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
        player.enabled = true;
        player.isPlayerControlled = true;
        player.health = 30;
        player.criticallyInjured = false;
        player.hasBeenCriticallyInjured = false;
        player.bleedingHeavily = false;
        player.isClimbingLadder = false;
        player.inSpecialInteractAnimation = false;
        player.disableSyncInAnimation = false;
        player.inVehicleAnimation = false;
        player.freeRotationInInteractAnimation = false;
        player.clampLooking = false;
        player.inShockingMinigame = false;
        player.currentTriggerInAnimationWith = null;
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
        player.parentedToElevatorLastFrame = false;
        player.isInGameOverAnimation = 0;
        player.overrideGameOverSpectatePivot = null;
        player.holdingWalkieTalkie = player.speakingToWalkieTalkie = false;
        player.voiceMuffledByEnemy = false;
        player.isHoldingObject = false;
        player.twoHanded = player.twoHandedAnimation = false;
        player.currentlyHeldObjectServer = null;
        player.carryWeight = 1;
        player.thisController.enabled = true;
        if (player.movementAudio != null) player.movementAudio.enabled = true;
        player.deadBody = null;
        if (body != null) { body.gameObject.SetActive(false); UnityEngine.Object.Destroy(body.gameObject); }
        // Inventory is verified empty before use. Never spawn, destroy, or clear inventory items here.
        round.livingPlayers = round.allPlayerScripts.Count(p => p != null && p.isPlayerControlled && !p.isPlayerDead);
        round.allPlayersDead = round.livingPlayers == 0;
        // Cosmetic/HUD mod failures must not prevent peers receiving the committed alive state.
        Present(() => {
            AccessTools.Method(typeof(PlayerControllerB), "ResetFallGravity").Invoke(player, Array.Empty<object>());
            player.ResetZAndXRotation();
            player.DisableJetpackControlsLocally();
            player.Crouch(false);
            player.ResetPlayerBloodObjects();
        });
        Present(() => {
            RestoreAnimator(player, round);
            player.DisablePlayerModel(player.gameObject, true, true);
            // The local body must remain enabled for animation/footstep events; shadows-only hides it from its own view.
            foreach (var model in new[] { player.thisPlayerModel, player.thisPlayerModelLOD1, player.thisPlayerModelLOD2 })
                if (model != null) { model.enabled = true; model.shadowCastingMode = player.IsOwner ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On; }
        });
        if (player.IsOwner) Present(() =>
        {
            SetInternal(player, "hasBegunSpectating", false);
            player.spectatedPlayerScript = null;
            // PlayerControllerB.Update must run its native take-control branch on the next frame.
            // Setting this false here previously skipped animator-controller and listener restoration.
            SetInternal(player, "isCameraDisabled", true);
            player.gameplayCamera.enabled = true;
            player.thisPlayerModelArms.enabled = true;
            AccessTools.Method(typeof(PlayerControllerB), "ChangeAudioListenerToObject").Invoke(player,
                new object[] { player.gameplayCamera.gameObject });
            round.SetSpectateCameraToGameOverMode(false, player);
            round.SwitchCamera(player.gameplayCamera);
            HUDManager.Instance.HideHUD(false);
            HUDManager.Instance.RemoveSpectateUI();
            if (HUDManager.Instance.audioListenerLowPass != null) HUDManager.Instance.audioListenerLowPass.enabled = false;
            HUDManager.Instance.UpdateHealthUI(30, false);
            HUDManager.Instance.gameOverAnimator.SetTrigger("revive");
            player.statusEffectAudio.Stop();
            if (SoundManager.Instance != null) SoundManager.Instance.SetDiageticMixerSnapshot(0, .1f);
        });
        Present(() => RepairVoice(player));
        Plugin.Instance.StartCoroutine(DeferredVoiceRepair(player));
        Plugin.Log($"Ciriolator revive applied: client={player.actualClientId}, owner={player.IsOwner}, alive={!player.isPlayerDead}, controlled={player.isPlayerControlled}.");
    }

    private static void RestoreAnimator(PlayerControllerB player, StartOfRound round)
    {
        var animator = player.playerBodyAnimator;
        var expected = player.IsOwner ? round.localClientAnimatorController : round.otherClientsAnimatorController;
        if (animator.runtimeAnimatorController == null || animator.runtimeAnimatorController == round.localClientAnimatorController ||
            animator.runtimeAnimatorController == round.otherClientsAnimatorController)
            animator.runtimeAnimatorController = expected;
        animator.enabled = true; animator.speed = 1;
        if (player.IsOwner) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind(); animator.Update(0);
        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool &&
                new[] { "Limp", "Walking", "Sprinting", "Sideways", "crouching", "Jumping", "FallNoJump", "HoldLung" }.Contains(parameter.name))
                animator.SetBool(parameter.nameHash, false);
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == "SA_stopAnimation")
                animator.SetTrigger(parameter.nameHash);
        }
        SetField(player, "currentAnimationStateHash", Enumerable.Repeat(0, animator.layerCount).ToList());
        SetField(player, "previousAnimationStateHash", Enumerable.Repeat(int.MinValue, animator.layerCount).ToList());
        SetField(player, "updatePlayerAnimationsInterval", 1f);
    }

    private static IEnumerator DeferredVoiceRepair(PlayerControllerB player)
    {
        // Dissonance playback bindings can arrive a frame later than the revive message.
        yield return null;
        if (player == null || player.isPlayerDead) yield break;
        Present(() => RepairVoice(player));
        yield return new WaitForSeconds(1);
        if (player != null && !player.isPlayerDead)
        {
            Present(() => RepairVoice(player));
            Plugin.Log($"Ciriolator post-revive check: client={player.actualClientId}, owner={player.IsOwner}, animator={(player.playerBodyAnimator != null ? player.playerBodyAnimator.runtimeAnimatorController?.name : "missing")}, listenerOnPlayer={player.activeAudioListener != null && player.activeAudioListener.transform.IsChildOf(player.transform)}.");
        }
    }

    private static void RepairVoice(PlayerControllerB player)
    {
        var round = StartOfRound.Instance;
        if (round == null || player == null || player.isPlayerDead) return;
        player.voiceMuffledByEnemy = false;
        round.RefreshPlayerVoicePlaybackObjects();
        if (player.currentVoiceChatIngameSettings != null)
        {
            if (player.currentVoiceChatIngameSettings.voiceAudio == null) player.currentVoiceChatIngameSettings.InitializeComponents();
            player.currentVoiceChatIngameSettings.set2D = false;
        }
        if (player.currentVoiceChatAudioSource != null)
        {
            player.currentVoiceChatAudioSource.enabled = true;
            var occlusion = player.currentVoiceChatAudioSource.GetComponent<OccludeAudio>();
            if (occlusion != null) occlusion.overridingLowPass = false;
        }
        // Volume is the game's alive/dead gain, not the user's IsLocallyMuted preference.
        if (!player.IsOwner && player.voicePlayerState != null) player.voicePlayerState.Volume = 1;
        var slot = Array.IndexOf(round.allPlayerScripts, player);
        var sound = SoundManager.Instance;
        if (sound != null && slot >= 0 && slot < sound.playerVoicePitchTargets.Length)
        { sound.playerVoicePitchTargets[slot] = 1; sound.SetPlayerPitch(1, slot); }
        round.UpdatePlayerVoiceEffects();
    }

    private static void Present(Action action)
    {
        try { action(); }
        catch (Exception e) { Plugin.Error($"Player revived, but a presentation/voice integration failed: {e}"); }
    }

    private static void SetInternal(PlayerControllerB player, string name, bool value)
        => SetField(player, name, value);

    private static void SetField(PlayerControllerB player, string name, object value)
    {
        // These fields can be private in the shipped game although publicized reference snapshots expose them.
        var field = AccessTools.Field(typeof(PlayerControllerB), name);
        if (field != null) field.SetValue(player, value);
        else Plugin.Error($"Game API mismatch: PlayerControllerB.{name} was not found.");
    }
}
