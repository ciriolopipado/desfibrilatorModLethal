using System;
using System.Collections.Generic;
using LethalLib.Modules;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Desfibrilator;

internal static class ItemFactory
{
    internal const int ShopPrice = 50;
    internal static AudioClip Charge = null!, Discharge = null!;
    internal static void Register()
    {
        Charge = MakeSound("DesfibrilatorCharge", 5f, false);
        Discharge = MakeSound("DesfibrilatorDischarge", 0.28f, true);
        var prefab = LethalLib.Modules.NetworkPrefabs.CreateNetworkPrefab("DesfibrilatorPrefab");
        prefab.tag = "PhysicsProp";
        prefab.layer = 6;
        var collider = prefab.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.34f, 0.22f, 0.25f);
        var item = ScriptableObject.CreateInstance<Item>();
        item.name = "DesfibrilatorItem";
        item.itemName = "Ciriolator";
        item.itemId = 198481;
        item.creditsWorth = ShopPrice;
        item.weight = 1.15f;
        item.isScrap = false;
        item.requiresBattery = false;
        item.canBeGrabbedBeforeGameStart = true;
        item.canBeInspected = true;
        item.twoHanded = false;
        item.twoHandedAnimation = true;
        item.isConductiveMetal = true;
        item.spawnPositionTypes = new List<ItemGroup>();
        item.toolTips = new[] { "Desfibrilar: mantener E durante 5 s" };
        item.grabAnim = "HoldClipboard";
        item.pocketAnim = "";
        item.useAnim = "";
        item.throwAnim = "";
        item.grabAnimationTime = 0.3f;
        item.rotationOffset = new Vector3(0, 0, 0);
        item.positionOffset = new Vector3(0.08f, 0.02f, -0.12f);
        item.restingRotation = Vector3.zero;
        item.verticalOffset = 0.12f;
        item.highestSalePercentage = 0; // Fixed official price: no vanilla shop discounts.
        item.itemIcon = MakeIcon();
        item.spawnPrefab = prefab;

        var tool = prefab.AddComponent<DefibrillatorItem>();
        tool.itemProperties = item;
        tool.grabbable = true;
        tool.grabbableToEnemies = false;
        tool.propColliders = new Collider[] { collider };
        tool.Visual = new GameObject("DesfibrilatorModel").transform;
        tool.Visual.SetParent(prefab.transform, false);
        var orange = new Color(0.9f, 0.34f, 0.04f);
        var dark = new Color(0.07f, 0.08f, 0.08f);
        var pale = new Color(0.78f, 0.76f, 0.64f);
        tool.mainObjectRenderer = Part(tool.Visual, "Carcasa", Vector3.zero, new Vector3(.34f, .20f, .24f), orange);
        Part(tool.Visual, "Panel", new Vector3(0, .105f, 0), new Vector3(.29f, .015f, .20f), pale);
        Part(tool.Visual, "Pantalla", new Vector3(-.055f, .12f, 0), new Vector3(.12f, .015f, .09f), dark);
        Part(tool.Visual, "Indicador", new Vector3(-.055f, .13f, 0), new Vector3(.085f, .006f, .009f), Color.green);
        Part(tool.Visual, "CruzHorizontal", new Vector3(.08f, .12f, 0), new Vector3(.065f, .012f, .02f), Color.red);
        Part(tool.Visual, "CruzVertical", new Vector3(.08f, .12f, 0), new Vector3(.02f, .012f, .065f), Color.red);
        Part(tool.Visual, "Asa", new Vector3(0, .04f, -.155f), new Vector3(.18f, .04f, .03f), dark);
        tool.LeftPaddle = Paddle(tool.Visual, "PalaIzquierda", new Vector3(-.21f, 0, 0), pale, dark);
        tool.RightPaddle = Paddle(tool.Visual, "PalaDerecha", new Vector3(.21f, 0, 0), pale, dark);
        Cable(tool.Visual, -.17f, -.21f, dark);
        Cable(tool.Visual, .17f, .21f, dark);
        var audio = prefab.AddComponent<AudioSource>();
        audio.spatialBlend = 1;
        audio.minDistance = 1;
        audio.maxDistance = 15;
        audio.rolloffMode = AudioRolloffMode.Linear;
        audio.playOnAwake = false;
        tool.Sound = audio;
        Utilities.FixMixerGroups(prefab);
        Items.RegisterShopItem(item, ShopPrice);
    }

    private static Transform Paddle(Transform parent, string name, Vector3 position, Color pale, Color dark)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false); root.localPosition = position;
        Part(root, "Electrodo", Vector3.zero, new Vector3(.065f, .018f, .10f), pale);
        Part(root, "Empunadura", new Vector3(0, .035f, 0), new Vector3(.035f, .05f, .07f), dark);
        return root;
    }

    private static void Cable(Transform root, float from, float to, Color color)
    {
        Part(root, "Cable", new Vector3((from + to) / 2, -.025f, -.07f), new Vector3(Math.Abs(to - from) + .01f, .012f, .012f), color);
        Part(root, "CableRetorno", new Vector3(to, -.01f, -.04f), new Vector3(.012f, .012f, .07f), color);
    }

    private static MeshRenderer Part(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name; obj.layer = 6;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position; obj.transform.localScale = scale;
        Object.DestroyImmediate(obj.GetComponent<Collider>());
        obj.AddComponent<PartTint>().Tint = color;
        return obj.GetComponent<MeshRenderer>();
    }

    private static Sprite MakeIcon()
    {
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var pixels = new Color[64 * 64];
        for (var y = 0; y < 64; y++) for (var x = 0; x < 64; x++)
        {
            var c = Color.clear;
            if (x >= 8 && x < 56 && y >= 12 && y < 50) c = new Color(.95f, .4f, .05f);
            if (x >= 14 && x < 32 && y >= 24 && y < 40) c = new Color(.1f, .2f, .15f);
            if ((x >= 39 && x < 44 && y >= 25 && y < 42) || (x >= 34 && x < 49 && y >= 31 && y < 36)) c = Color.white;
            pixels[y * 64 + x] = c;
        }
        texture.SetPixels(pixels); texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64);
    }

    private static AudioClip MakeSound(string name, float seconds, bool discharge)
    {
        const int rate = 22050;
        var data = new float[(int)(seconds * rate)];
        var random = new System.Random(81);
        double phase = 0;
        for (var i = 0; i < data.Length; i++)
        {
            double t = (double)i / rate, progress = t / seconds;
            phase += 2 * Math.PI * (discharge ? 110 : 180 + 1100 * progress) / rate;
            double envelope = discharge ? Math.Exp(-progress * 8) : Math.Min(1, t * 12) * Math.Min(1, (seconds - t) * 15);
            data[i] = (float)(envelope * (discharge ? (random.NextDouble() * 2 - 1) * .55 + Math.Sin(phase) * .15 : Math.Sin(phase) * .16));
        }
        var clip = AudioClip.Create(name, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

public sealed class PartTint : MonoBehaviour
{
    public Color Tint;
    private Material? material;
    private void Start()
    {
        var renderer = GetComponent<MeshRenderer>();
        var shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard") ?? renderer.sharedMaterial.shader;
        material = new Material(shader);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Tint);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Tint);
        renderer.sharedMaterial = material;
    }
    private void OnDestroy() { if (material != null) Destroy(material); }
}
