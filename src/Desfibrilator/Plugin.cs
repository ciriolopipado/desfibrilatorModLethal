using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using GameNetcodeStuff;

namespace Desfibrilator;

[BepInPlugin(Id, "Ciriolator", "1.0.0")]
[BepInDependency("evaisa.lethallib", "1.2.0")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Id = "ciriolopipado.desfibrilator";
    internal static Plugin Instance = null!;
    internal static InputAction Use = null!;
    private Harmony? harmony;

    private void Awake()
    {
        Instance = this;
        ConfigEntry<string> binding = Config.Bind("Controls", "Defibrillate", "<Keyboard>/e", "Input System binding path. Restart the game after changing it.");
        Use = new InputAction("Ciriolator", InputActionType.Button, binding.Value);
        Use.Enable();
        ItemFactory.Register();
        gameObject.AddComponent<ReviveNetwork>();
        gameObject.AddComponent<RevivalSideEffects>();
        harmony = new Harmony(Id);
        harmony.PatchAll();
        Logger.LogInfo("Ciriolator 1.0.0 loaded. Target: Lethal Company v81. All peers need the mod.");
    }

    private void OnDestroy() { harmony?.UnpatchSelf(); Use?.Dispose(); }
    internal static void Error(object message) => Instance.Logger.LogError(message);
    internal static void Log(object message) => Instance.Logger.LogInfo(message);
}

// Suppress vanilla corpse pickup only while the tool is active and the crosshair is on a corpse.
// Ordinary interaction is untouched when another item is equipped, or when aiming elsewhere.
[HarmonyPatch(typeof(PlayerControllerB), "BeginGrabObject")]
internal static class CorpseInteractionPatch
{
    private static bool Prefix(PlayerControllerB __instance) =>
        !Interaction.HasActiveTool(__instance) || Interaction.FindAimedBody(__instance) == null;
}

[HarmonyPatch(typeof(PlayerControllerB), "SetHoverTipAndCurrentInteractTrigger")]
internal static class CorpseTooltipPatch
{
    private static void Postfix(PlayerControllerB __instance)
    {
        if (!__instance.IsOwner || !Interaction.HasActiveTool(__instance)) return;
        var body = Interaction.FindAimedBody(__instance);
        if (body == null) return;
        var tip = AccessTools.Field(typeof(PlayerControllerB), "cursorTip").GetValue(__instance);
        if (tip == null) return;
        var text = Interaction.IsRecoverable(body)
            ? "Desfibrilar: mantener la tecla configurada (E por defecto)"
            : "Cadáver no recuperable";
        AccessTools.Property(tip.GetType(), "text").SetValue(tip, text);
    }
}
