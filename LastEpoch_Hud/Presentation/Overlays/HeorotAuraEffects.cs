using System;
using Il2Cpp;
using LastEpoch_Hud.Scripts;
using LastEpoch_Hud.Scripts.Mods.Character.Heorot;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Presentation.Overlays;

internal static class HeorotAuraEffects
{
    // Power uses the former Defence visual, Energy the former Power, Defence the former Energy.
    private static readonly int[] VisualOrder = { 2, 0, 1 };
    private static readonly GameObject[] shards = new GameObject[3];
    private static readonly TrailRenderer[] trails = new TrailRenderer[3];
    private static readonly SpriteRenderer[] frost = new SpriteRenderer[24];
    private static GameObject root;
    private static Actor owner;
    private static Material spriteMaterial;
    private static Material trailMaterial;
    private static SpriteRenderer groundFrost;
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
        for (int i = 0; i < shards.Length; i++)
            if (HeorotBuffs.IsActive(actor, (HeorotBuffKind)i)) mask |= 1 << i;
        if (mask == 0) { Clear(); return; }
        if (unavailable) return;
        if (owner != actor) Clear();
        if (root == null)
        {
            try { Create(actor); }
            catch (Exception ex)
            {
                Clear();
                unavailable = true;
                Main.logger_instance?.Warning("Heorot aura: " + ex.Message);
                return;
            }
        }
        Vector3 position = actor.transform.position;
        if ((root.transform.position - position).sqrMagnitude > 16f)
            foreach (var trail in trails) if (trail != null) trail.Clear();
        root.transform.position = position;
        Animate(mask, Time.time, Camera.main);
    }

    private static void Animate(int mask, float time, Camera camera)
    {
        int activeCount = 0;
        for (int i = 0; i < shards.Length; i++)
        {
            bool active = (mask & (1 << i)) != 0;
            int visual = VisualOrder[i];
            float angle = (time * 52f + visual * 120f) * Mathf.Deg2Rad;
            shards[i].transform.localPosition = new Vector3(Mathf.Cos(angle) * .78f,
                1.03f + Mathf.Sin(time * 1.5f + visual * 2f) * .09f, Mathf.Sin(angle) * .78f);
            // Camera-facing crystal art keeps the detailed facets readable at gameplay distance.
            Quaternion facing = camera != null ? camera.transform.rotation : Quaternion.identity;
            shards[i].transform.rotation = facing * Quaternion.Euler(0, 0,
                visual * 17f - 18f + time * (visual == 1 ? -13f : 11f));
            if (shards[i].activeSelf != active)
            {
                shards[i].SetActive(active);
                trails[i].Clear();
            }
            if (active) activeCount++;
        }
        float intensity = .7f + activeCount * .15f;
        groundFrost.color = new Color(.84f, .94f, 1f,
            (.19f + .025f * activeCount) * (1f + .07f * Mathf.Sin(time * 1.2f)));
        groundFrost.transform.localRotation = Quaternion.Euler(90, 0, time * 3f);
        for (int i = 0; i < frost.Length; i++)
        {
            bool mote = i % 4 == 0;
            float life = Mathf.Repeat(time * (mote ? .28f : .18f) + i * .618034f, 1f);
            float angle = time * .22f + i * 2.39996f + life * .6f;
            float radius = .22f + .38f * (i % 7 / 6f) + life * .16f;
            var particle = frost[i];
            particle.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius,
                .10f + life * (mote ? 1.55f : .80f), Mathf.Sin(angle) * radius);
            Quaternion facing = camera != null ? camera.transform.rotation : Quaternion.identity;
            particle.transform.rotation = facing * Quaternion.Euler(0, 0, i * 43f + life * 25f);
            float height = (mote ? .035f : .72f) * (.75f + life * .5f);
            float scale = height / particle.sprite.bounds.size.y;
            particle.transform.localScale = new Vector3(scale, scale, scale);
            particle.color = new Color(.88f, .95f, 1f,
                Mathf.Pow(Mathf.Sin(life * Mathf.PI), 1.5f) * (mote ? .6f : .12f) * intensity);
        }
    }

    private static Shader FindShader(params string[] names)
    {
        foreach (string name in names)
        {
            var shader = Shader.Find(name);
            if (shader != null && shader.isSupported) return shader;
        }
        throw new InvalidOperationException("No supported frost sprite shader is loaded.");
    }

    private static void Create(Actor actor)
    {
        var shardArt = OrbArt.Sheet("heorot_ice_fragments_v2", 3, 1);
        var mistArt = OrbArt.Named("heorot_frost_mist_v2");
        var floorArt = OrbArt.Named("heorot_ground_frost_v2");
        if (shardArt.Length != 3 || shardArt[0] == null || shardArt[1] == null || shardArt[2] == null
            || mistArt == null || floorArt == null)
            throw new InvalidOperationException("Generated Heorot frost textures are missing.");
        owner = actor;
        root = new GameObject("Heorot_Aura") { hideFlags = HideFlags.HideAndDontSave };
        root.transform.position = actor.IsNullOrDestroyed() ? Vector3.zero : actor.transform.position;
        spriteMaterial = new Material(FindShader("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        trailMaterial = new Material(FindShader("Legacy Shaders/Particles/Additive", "Particles/Additive", "Sprites/Default"))
        {
            hideFlags = HideFlags.HideAndDontSave, mainTexture = mistArt.texture,
        };
        if (trailMaterial.HasProperty("_TintColor")) trailMaterial.SetColor("_TintColor", Color.white);
        for (int i = 0; i < shards.Length; i++)
        {
            var renderer = SpriteObject("Heorot_" + (HeorotBuffKind)i + "_IceFragment", shardArt[VisualOrder[i]]);
            shards[i] = renderer.gameObject;
            SetHeight(renderer, .64f);
            shards[i].SetActive(false);
            var trail = shards[i].AddComponent<TrailRenderer>();
            trails[i] = trail;
            trail.sharedMaterial = trailMaterial;
            trail.time = .38f;
            trail.minVertexDistance = .035f;
            trail.startWidth = .12f;
            trail.endWidth = .025f;
            trail.startColor = new Color(.70f, .88f, 1f, .14f);
            trail.endColor = Color.clear;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }
        groundFrost = SpriteObject("Heorot_GlacialFrost", floorArt);
        SetHeight(groundFrost, 2.1f);
        groundFrost.transform.localPosition = new Vector3(0, .045f, 0);
        groundFrost.transform.localRotation = Quaternion.Euler(90, 0, 0);
        for (int i = 0; i < frost.Length; i++)
            frost[i] = SpriteObject(i % 4 == 0 ? "Heorot_IceMote" : "Heorot_FreezingVapor",
                i % 4 == 0 ? shardArt[i % 3] : mistArt);
        Animate(7, 0, Camera.main);
    }

    private static SpriteRenderer SpriteObject(string name, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = spriteMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static void SetHeight(SpriteRenderer renderer, float height)
    {
        float scale = height / renderer.sprite.bounds.size.y;
        renderer.transform.localScale = new Vector3(scale, scale, scale);
    }

    internal static void Clear()
    {
        if (root != null) Object.Destroy(root);
        if (spriteMaterial != null) Object.Destroy(spriteMaterial);
        if (trailMaterial != null) Object.Destroy(trailMaterial);
        for (int i = 0; i < shards.Length; i++) { shards[i] = null; trails[i] = null; }
        for (int i = 0; i < frost.Length; i++) frost[i] = null;
        root = null;
        groundFrost = null;
        owner = null;
        spriteMaterial = null;
        trailMaterial = null;
        unavailable = false;
        // OrbArt owns the cached textures/sprites and keeps them for subsequent buff activations.
    }

    internal static string Inspect()
    {
        bool temporary = root == null;
        try
        {
            if (temporary) Create(null);
            return "Generated frost VFX initialized: three distinct textured ice fragments, "
                + frost.Length + " mist/mote sprites, glacial ground frost and three vapor trails. Shader="
                + spriteMaterial.shader.name;
        }
        finally { if (temporary) Clear(); }
    }
}
