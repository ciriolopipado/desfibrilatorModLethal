using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Rendering;

namespace Desfibrilator;

/// <summary>Short original electric arcs around the torso, rendered locally from a shared shock event.</summary>
public sealed class ShockSparkBurst : MonoBehaviour
{
    private readonly LineRenderer[] arcs = new LineRenderer[10];
    private readonly Vector3[] points = new Vector3[6];
    private readonly System.Random random = new System.Random();
    private Material? material;
    private float ends, redraw;

    internal static ShockSparkBurst? Spawn(PlayerControllerB player)
    {
        var shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader == null) { Plugin.Error("Ciriolator sparks unavailable: no supported unlit shader."); return null; }
        var root = new GameObject("CiriolatorElectricSparks");
        // Attach to this player's spine, never to a camera, HUD or an observer's object.
        // The root offset is only a fallback for replacement models without the native bone.
        var anchor = player.upperSpine != null ? player.upperSpine : player.transform;
        root.transform.SetParent(anchor, false);
        root.transform.localPosition = anchor == player.transform ? Vector3.up * 1.1f : Vector3.zero;
        var burst = root.AddComponent<ShockSparkBurst>();
        burst.material = new Material(shader);
        var cyan = new Color(.2f, .8f, 1f, 1f);
        if (burst.material.HasProperty("_UnlitColor")) burst.material.SetColor("_UnlitColor", cyan);
        if (burst.material.HasProperty("_Color")) burst.material.SetColor("_Color", cyan);
        if (burst.material.HasProperty("_EmissiveColor")) burst.material.SetColor("_EmissiveColor", cyan * 5);
        for (var i = 0; i < burst.arcs.Length; i++)
        {
            var obj = new GameObject("ElectricArc"); obj.transform.SetParent(root.transform, false);
            var arc = obj.AddComponent<LineRenderer>(); burst.arcs[i] = arc;
            arc.sharedMaterial = burst.material; arc.useWorldSpace = false;
            arc.positionCount = burst.points.Length;
            arc.startWidth = .018f; arc.endWidth = .006f;
            arc.startColor = Color.white; arc.endColor = cyan;
            arc.shadowCastingMode = ShadowCastingMode.Off; arc.receiveShadows = false;
        }
        burst.ends = Time.realtimeSinceStartup + (float)Desfibrilator.Core.ShockImpulse.WalkSeconds;
        burst.Draw();
        return burst;
    }

    private void LateUpdate()
    {
        if (Time.realtimeSinceStartup >= ends) { Destroy(gameObject); return; }
        if (Time.realtimeSinceStartup >= redraw) Draw();
    }

    private void Draw()
    {
        redraw = Time.realtimeSinceStartup + .055f;
        foreach (var arc in arcs)
        {
            var angle = (float)(random.NextDouble() * Mathf.PI * 2);
            var start = new Vector3(Mathf.Sin(angle) * .22f, Range(-.4f, .45f), Mathf.Cos(angle) * .22f);
            var end = start + new Vector3(Range(-.4f, .4f), Range(-.35f, .35f), Range(-.4f, .4f));
            for (var j = 0; j < points.Length; j++)
                points[j] = Vector3.Lerp(start, end, j / (float)(points.Length - 1)) +
                    (j == 0 || j == points.Length - 1 ? Vector3.zero : new Vector3(Range(-.07f, .07f), Range(-.07f, .07f), Range(-.07f, .07f)));
            arc.SetPositions(points);
        }
    }

    private float Range(float min, float max) => (float)(min + random.NextDouble() * (max - min));
    private void OnDestroy() { if (material != null) Destroy(material); }
}
