using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Desfibrilator;

// Insert into the native input read, before the game's movement/animation/physics processing.
// Leave all native menu and interaction checks in place and never move remote player transforms.
[HarmonyPatch(typeof(PlayerControllerB), "Update")]
internal static class ConvulsionInputPatch
{
    internal static bool Installed;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = new List<CodeInstruction>(instructions);
        var readIndex = -1;
        var moveField = AccessTools.Field(typeof(PlayerControllerB), "moveInputVector");
        for (var i = 0; i + 1 < code.Count; i++)
        {
            if ((code[i].opcode == OpCodes.Call || code[i].opcode == OpCodes.Callvirt) &&
                code[i].operand is MethodInfo method && method.DeclaringType == typeof(InputAction) &&
                method.Name == "ReadValue" && method.ReturnType == typeof(Vector2) &&
                code[i + 1].opcode == OpCodes.Stfld && Equals(code[i + 1].operand, moveField))
            {
                if (readIndex >= 0) { readIndex = -1; break; }
                readIndex = i;
            }
        }
        Installed = readIndex >= 0;
        if (Installed)
        {
            code.InsertRange(readIndex + 1, new[] {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ConvulsionInputPatch), nameof(Filter)))
            });
            Plugin.Log("Ciriolator convulsion input hook installed.");
        }
        else Plugin.Error("Ciriolator convulsion input hook unavailable: native Move read changed. Involuntary walking disabled; other effects remain enabled.");
        return code;
    }

    private static Vector2 Filter(Vector2 input, PlayerControllerB player) =>
        RevivalSideEffects.Instance != null ? RevivalSideEffects.Instance.FilterMovement(input, player) : input;
}
