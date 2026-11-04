using System.Linq;
using GameNetcodeStuff;
using UnityEngine;

namespace Desfibrilator;

internal static class Interaction
{
    internal const float Range = 2.5f;
    internal static bool HasActiveTool(PlayerControllerB? player) => player != null &&
        !player.isPlayerDead && player.currentlyHeldObjectServer is DefibrillatorItem tool &&
        tool.isHeld && !tool.isPocketed && tool.playerHeldBy == player;

    internal static DeadBodyInfo? FindAimedBody(PlayerControllerB player)
    {
        if (player.gameplayCamera == null) return null;
        var ray = new Ray(player.gameplayCamera.transform.position, player.gameplayCamera.transform.forward);
        foreach (var hit in Physics.RaycastAll(ray, Range, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
        {
            if (hit.collider.transform.IsChildOf(player.transform)) continue;
            var ownTool = hit.collider.GetComponentInParent<DefibrillatorItem>();
            if (ownTool != null && ownTool.playerHeldBy == player) continue;
            var body = hit.collider.GetComponentInParent<DeadBodyInfo>();
            if (body == null)
            {
                var ragdoll = hit.collider.GetComponentInParent<RagdollGrabbableObject>();
                if (ragdoll != null) body = ragdoll.ragdoll;
            }
            if (body != null) return body;
            // Ignore triggers (weather, interaction volumes), but never aim through solid geometry.
            if (!hit.collider.isTrigger) return null;
        }
        return null;
    }

    internal static Vector3 BodyPosition(DeadBodyInfo body) =>
        body.bodyParts != null && body.bodyParts.Length > 0 && body.bodyParts[0] != null
            ? body.bodyParts[0].position : body.transform.position;

    internal static bool IsRecoverable(DeadBodyInfo? body)
    {
        if (body == null || !body.gameObject.activeInHierarchy || body.deactivated || body.detachedHead ||
            body.attachedTo != null || body.secondaryAttachedTo != null) return false;
        var target = body.playerScript;
        var grab = body.grabBodyObject;
        return target != null && target.isPlayerDead && target.deadBody == body && grab != null &&
            grab.IsSpawned && grab.grabbable && !grab.isHeld && !grab.isHeldByEnemy &&
            body.bodyParts != null && body.bodyParts.Length > 0 &&
            body.bodyParts.All(p => p != null && p.gameObject.activeInHierarchy) &&
            target.ItemSlots != null && target.ItemSlots.All(i => i == null) && target.ItemOnlySlot == null;
    }

    internal static bool RoundActive => StartOfRound.Instance != null &&
        !StartOfRound.Instance.inShipPhase && !StartOfRound.Instance.firingPlayersCutsceneRunning;
}
