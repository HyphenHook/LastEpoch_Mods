using System;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LastEpoch_Hud.Scripts;
using LastEpoch_Hud.Scripts.Core.Volcanic;
using LastEpoch_Hud.Scripts.Mods.Character.Volcanic;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using LastEpoch_Hud.Scripts.Mods.Items.Jewels;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Presentation.Overlays;

internal static class VolcanicAuraEffects
{
    private static readonly GameObject[] groups = new GameObject[3];
    private static readonly SpriteRenderer[] essences = new SpriteRenderer[3];
    private static readonly SpriteRenderer[] fragments = new SpriteRenderer[8];
    private static readonly ParticleSystem[] essenceTrails = new ParticleSystem[3];
    private static readonly ParticleSystem[] fragmentFire = new ParticleSystem[8];
    private static SpriteRenderer wrathGroundRing;
    private static SpriteRenderer ringNextFrame;
    private static ParticleSystem commonBodyHeat;
    private static ParticleSystem angerBodyFire;
    private static SpriteRenderer angerMagma;
    private static float magmaBaseScale;
    private static Material emberMaterial;
    private static Texture2D emberTexture;
    private static Sprite[] wrathRingFrames;
    private static Sprite[] flameFrames;
    private static Sprite[] essenceFrames;
    private static Sprite[] fragmentArt;
    private static GameObject root;
    private static Actor owner;
    private static Material spriteMaterial;
    private static Material fireMaterial;
    private static bool unavailable;

    internal static void Tick()
    {
        var actor = Refs_Manager.player_actor;
        var player = Refs_Manager.player_data;
        if (!Scenes.IsGameScene() || player.IsNullOrDestroyed() || !player.IsOffline
            || actor.IsNullOrDestroyed() || actor.health.IsNullOrDestroyed() || actor.health.currentHealth <= 0)
        {
            Clear();
            return;
        }
        int mask = 0;
        for (int i = 0; i < 3; i++)
            if (VolcanicBuffs.IsActive(actor, (VolcanicBuffKind)i)) mask |= 1 << i;
        bool equippedCore = false;
        for (int slot = 0; slot < JewelEquipment.SlotCount; slot++)
            equippedCore |= VolcanicCore.IsCore(JewelEquipment.EquippedAt(slot));
        if (!VolcanicVisualRules.HasAura(equippedCore, mask)) { Clear(); return; }
        if (unavailable) return;
        if (owner != actor) Clear();
        if (root == null)
        {
            try { Create(actor); }
            catch (Exception ex)
            {
                Clear();
                unavailable = true;
                Main.logger_instance?.Warning("Volcanic aura: " + ex.Message);
                return;
            }
        }
        if ((root.transform.position - actor.transform.position).sqrMagnitude > 16f)
        {
            commonBodyHeat.Clear();
            angerBodyFire.Clear();
            foreach (var trail in essenceTrails) trail.Clear();
            foreach (var fire in fragmentFire) fire.Clear();
        }
        root.transform.position = actor.transform.position;
        Animate(mask, Time.time, Camera.main);
    }

    private static Shader FindShader(params string[] names)
    {
        foreach (string name in names)
        {
            var shader = Shader.Find(name);
            if (shader != null && shader.isSupported) return shader;
        }
        throw new InvalidOperationException("Volcanic particle shader unavailable.");
    }

    private static void Create(Actor actor)
    {
        flameFrames = OrbArt.Sheet("volcanic_flame_flipbook_v3", 4, 4);
        essenceFrames = OrbArt.Sheet("volcanic_essence_flipbook_v3", 4, 4);
        fragmentArt = OrbArt.Sheet("volcanic_vessel_shards_v4", 4, 2);
        wrathRingFrames = OrbArt.Sheet("volcanic_burning_ring_v6", 8, 8);
        if (Array.Exists(flameFrames, frame => frame == null) || Array.Exists(essenceFrames, frame => frame == null)
            || Array.Exists(fragmentArt, frame => frame == null) || Array.Exists(wrathRingFrames, frame => frame == null))
            throw new InvalidOperationException("Missing volcanic animation sheet.");
        owner = actor;
        root = new GameObject("Volcanic_Shaman_Auras") { hideFlags = HideFlags.HideAndDontSave };
        root.transform.position = actor.IsNullOrDestroyed() ? Vector3.zero : actor.transform.position;
        spriteMaterial = new Material(FindShader("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        fireMaterial = new Material(FindShader("Legacy Shaders/Particles/Alpha Blended", "Particles/Standard Unlit", "Particles/Alpha Blended"))
        {
            hideFlags = HideFlags.HideAndDontSave, mainTexture = flameFrames[0].texture, renderQueue = 3000,
        };
        if (fireMaterial.HasProperty("_SrcBlend")) fireMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (fireMaterial.HasProperty("_DstBlend")) fireMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (fireMaterial.HasProperty("_ZWrite")) fireMaterial.SetInt("_ZWrite", 0);
        if (fireMaterial.HasProperty("_TintColor")) fireMaterial.SetColor("_TintColor", new Color(.5f, .5f, .5f, 1));
        fireMaterial.mainTextureScale = new Vector2(.25f, .25f);
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i] = new GameObject("Volcanic_" + (VolcanicBuffKind)i);
            groups[i].transform.SetParent(root.transform, false);
        }
        wrathGroundRing = MakeSprite("VolcanicCore_BurningRing", -1, wrathRingFrames[0], 1.73f);
        wrathGroundRing.transform.localPosition = Vector3.up * .045f;
        wrathGroundRing.transform.localRotation = Quaternion.Euler(90, 0, 0);
        ringNextFrame = MakeSprite("VolcanicCore_RingCrossfade", -1, wrathRingFrames[1], 1.73f);
        ringNextFrame.transform.localPosition = Vector3.up * .045f;
        ringNextFrame.transform.localRotation = Quaternion.Euler(90, 0, 0);
        ringNextFrame.sortingOrder = wrathGroundRing.sortingOrder + 1;
        commonBodyHeat = MakeFire("VolcanicCore_SubtleBodyHeat", root.transform, .11f, 5, .17f);
        commonBodyHeat.transform.localPosition = Vector3.up * .5f;
        var commonMain = commonBodyHeat.main;
        commonMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        var magmaArt = OrbArt.Named("volcanic_anger_magma_v7");
        if (magmaArt == null) throw new InvalidOperationException("Missing Anger magma texture.");
        angerMagma = MakeSprite("Anger_PulsingMagma", 0, magmaArt, 1.25f);
        angerMagma.transform.localPosition = Vector3.up * .035f;
        angerMagma.transform.localRotation = Quaternion.Euler(90, 0, 0);
        angerMagma.sortingOrder = -1;
        magmaBaseScale = angerMagma.transform.localScale.x;
        emberTexture = CreateEmberTexture();
        emberMaterial = new Material(FindShader("Legacy Shaders/Particles/Additive", "Particles/Additive", "Legacy Shaders/Particles/Alpha Blended"))
        {
            hideFlags = HideFlags.HideAndDontSave, mainTexture = emberTexture, renderQueue = 3000,
        };
        if (emberMaterial.HasProperty("_TintColor")) emberMaterial.SetColor("_TintColor", new Color(.5f, .5f, .5f, 1));
        angerBodyFire = MakeFire("Anger_HotBodyEmbers", groups[0].transform, .09f, 42, .38f);
        angerBodyFire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        angerBodyFire.transform.localPosition = Vector3.up * .85f;
        angerBodyFire.GetComponent<ParticleSystemRenderer>().sharedMaterial = emberMaterial;
        var angerMain = angerBodyFire.main;
        angerMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        angerMain.startLifetime = new ParticleSystem.MinMaxCurve(1.35f, 1.9f);
        angerMain.startSize = new ParticleSystem.MinMaxCurve(.055f, .10f);
        angerMain.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .65f, .16f, 1f), new Color(1f, .95f, .65f, 1f));
        angerMain.gravityModifier = new ParticleSystem.MinMaxCurve(-.09f);
        angerMain.maxParticles = 96;
        var emberSize = angerBodyFire.sizeOverLifetime;
        emberSize.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, .35f));
        angerBodyFire.Play();
        for (int i = 0; i < essences.Length; i++)
        {
            essences[i] = MakeSprite("Hunger_FlowingFireEssence_" + i, 1, essenceFrames[0], .46f);
            essenceTrails[i] = MakeFire("Hunger_EmberTrail_" + i, essences[i].transform, .08f, 6, .015f);
            // Parent sprites are scaled for pixels-per-unit; particle simulation uses hierarchy-independent size.
            var main = essenceTrails[i].main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }
        for (int i = 0; i < fragments.Length; i++)
        {
            fragments[i] = MakeSprite("Vessel_Shard_" + i, 2, fragmentArt[i], .28f + i % 3 * .03f);
            fragmentFire[i] = MakeFire("Vessel_ShardFlame_" + i, fragments[i].transform, .16f, 5, .012f);
            fragmentFire[i].transform.localPosition = new Vector3(0, fragmentArt[i].bounds.size.y * .15f, 0);
        }
        Animate(7, .25f, Camera.main);
    }

    private static ParticleSystem MakeFire(string name, Transform parent, float size, float rate, float radius)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.025f, .09f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * .7f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(.95f, .76f, .48f, .8f), new Color(1f, .95f, .75f, .95f));
        main.gravityModifier = new ParticleSystem.MinMaxCurve(-.025f);
        main.maxParticles = 24;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = system.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        var noise = system.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(.035f);
        noise.frequency = .5f;
        var fade = new Gradient();
        fade.SetKeys(new Il2CppStructArray<GradientColorKey>(new[]
        {
            new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1f, .82f, .55f), .55f),
            new GradientColorKey(new Color(.4f, .12f, .035f), 1),
        }), new Il2CppStructArray<GradientAlphaKey>(new[]
        {
            new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(0, 1),
        }));
        var colorLife = system.colorOverLifetime;
        colorLife.enabled = true;
        colorLife.color = new ParticleSystem.MinMaxGradient(fade);
        var sizeLife = system.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .65f, 1, 1.1f));
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = fireMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        system.Play();
        return system;
    }

    private static Texture2D CreateEmberTexture()
    {
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
        {
            name = "Anger_HotEmber", hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
        };
        var pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            float dx = (x + .5f) / 16f - 1;
            float dy = (y + .5f) / 16f - 1;
            float core = Mathf.Max(0, 1 - Mathf.Sqrt(dx * dx + dy * dy));
            pixels[y * 32 + x] = new Color(1, .15f + .85f * core, .015f + .6f * core, core * core);
        }
        texture.SetPixels(new Il2CppStructArray<Color>(pixels));
        texture.Apply(false, true);
        return texture;
    }

    private static SpriteRenderer MakeSprite(string name, int kind, Sprite sprite, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(kind < 0 ? root.transform : groups[kind].transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = spriteMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        float scale = height / sprite.bounds.size.y;
        go.transform.localScale = new Vector3(scale, scale, scale);
        return renderer;
    }

    private static void Animate(int mask, float time, Camera camera)
    {
        for (int i = 0; i < groups.Length; i++)
        {
            bool active = (mask & (1 << i)) != 0;
            if (groups[i].activeSelf == active) continue;
            groups[i].SetActive(active);
            if (i == 0) SetParticlesActive(angerBodyFire, active);
            else foreach (var system in i == 1 ? essenceTrails : fragmentFire) SetParticlesActive(system, active);
        }
        // This game strips TextureSheetAnimation setters. Animate the particle material UVs instead.
        int fireFrame = Mathf.FloorToInt(time * 12f) % 16;
        fireMaterial.mainTextureOffset = new Vector2(fireFrame % 4 * .25f, 1f - (fireFrame / 4 + 1) * .25f);
        var ring = VolcanicVisualRules.RingAnimation(time);
        var opacity = VolcanicVisualRules.RingOpacity(ring.Blend);
        wrathGroundRing.sprite = wrathRingFrames[ring.Current];
        ringNextFrame.sprite = wrathRingFrames[ring.Next];
        wrathGroundRing.color = new Color(.92f, .64f, .48f, opacity.Back);
        ringNextFrame.color = new Color(.92f, .64f, .48f, opacity.Front);
        float magmaPulse = .5f + .5f * Mathf.Sin(time * 2.1f);
        angerMagma.color = new Color(1f, .86f, .70f, .35f + magmaPulse * .28f);
        float magmaScale = magmaBaseScale * (1 + magmaPulse * .025f);
        angerMagma.transform.localScale = new Vector3(magmaScale, magmaScale, magmaScale);
        Quaternion facing = camera != null ? camera.transform.rotation : Quaternion.identity;
        for (int i = 0; i < essences.Length; i++)
        {
            float angle = time * .75f + i * Mathf.PI * 2f / essences.Length;
            var essence = essences[i];
            essence.transform.localPosition = new Vector3(Mathf.Cos(angle) * .62f,
                .74f + .06f * Mathf.Sin(time * 1.4f + i), Mathf.Sin(angle) * .62f);
            essence.transform.rotation = facing;
            essence.sprite = essenceFrames[(Mathf.FloorToInt(time * 12f) + i * 5) % essenceFrames.Length];
            essence.color = new Color(1f, .82f, .55f, .8f);
        }
        for (int i = 0; i < fragments.Length; i++)
        {
            float angle = -time * .48f + i * Mathf.PI * 2f / fragments.Length;
            float radius = .65f + i % 3 * .06f;
            fragments[i].transform.localPosition = new Vector3(Mathf.Cos(angle) * radius,
                .88f + i % 3 * .11f + .035f * Mathf.Sin(time + i), Mathf.Sin(angle) * radius);
            fragments[i].transform.rotation = facing;
            fragments[i].color = new Color(.72f, .60f, .53f, .9f);
        }
    }

    private static void SetParticlesActive(ParticleSystem system, bool active)
    {
        if (active) system.Play();
        else system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    internal static void Clear()
    {
        if (root != null) Object.Destroy(root);
        if (spriteMaterial != null) Object.Destroy(spriteMaterial);
        if (fireMaterial != null) Object.Destroy(fireMaterial);
        if (emberMaterial != null) Object.Destroy(emberMaterial);
        if (emberTexture != null) Object.Destroy(emberTexture);
        for (int i = 0; i < groups.Length; i++) groups[i] = null;
        for (int i = 0; i < essences.Length; i++) { essences[i] = null; essenceTrails[i] = null; }
        for (int i = 0; i < fragments.Length; i++) { fragments[i] = null; fragmentFire[i] = null; }
        root = null;
        owner = null;
        wrathGroundRing = null;
        ringNextFrame = null;
        commonBodyHeat = null;
        angerBodyFire = null;
        angerMagma = null;
        emberMaterial = null;
        emberTexture = null;
        spriteMaterial = null;
        fireMaterial = null;
        unavailable = false;
    }

    internal static string Inspect()
    {
        bool temporary = root == null;
        try
        {
            if (temporary) Create(null);
            return "All Volcanic Cores: crossfaded ring and subtle body heat. Anger: rising hot ember particles above pulsing magma. Hunger: circling essences. Vessel: burning shards. Ember shader=" + emberMaterial.shader.name;
        }
        finally { if (temporary) Clear(); }
    }
}
