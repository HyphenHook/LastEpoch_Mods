using System;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// Finds the game's own currency icons (Gold, Ancient Bones, Soul Ember, Memory Amber) for the crafting windows.
/// Searching every Image and Sprite in the game in one go froze the game for a moment the first time a window opened, so
/// the search runs a little each frame instead, starting a few seconds after a character loads. The windows only read the
/// result.
/// </summary>
internal static class IconSearch
{
    private const float StartDelaySeconds = 6f;
    private const double FrameBudgetMs = 1.5;
    private const int MaxPerFrame = 250;

    internal static Sprite Gold, Bones, Ember, EmberBuff, AmberFaction, AmberEcho;
    /// <summary>The Weaver faction's own Memory Amber sprite, or failing that the Echo reward one.</summary>
    internal static Sprite Amber => AmberFaction ?? AmberEcho;
    internal static bool Done { get; private set; }

    private static bool requested;
    private static float readySince = -1f;
    private static Il2CppArrayBase<Image> images;
    private static Il2CppArrayBase<Sprite> sprites;
    private static int imageAt, spriteAt;
    private static bool imagesDone, spritesDone;

    /// <summary>A window wants the icons: begin as soon as the game is ready instead of waiting for the delay.</summary>
    internal static void Request() => requested = true;

    internal static void Tick()
    {
        try
        {
            if (Done)
            {
                // Sprites can be unloaded with a scene: look again when one has gone.
                if (Dead(Gold) || Dead(Bones) || Dead(Ember ?? EmberBuff)) Restart();
                return;
            }
            if (!Scenes.IsGameScene() || Refs_Manager.player_actor.IsNullOrDestroyed())
            {
                readySince = -1f;
                return;
            }
            if (readySince < 0f) readySince = Time.unscaledTime;
            if (!requested && Time.unscaledTime - readySince < StartDelaySeconds) return;

            var clock = System.Diagnostics.Stopwatch.StartNew();
            int handled = 0;
            if (!imagesDone)
            {
                images ??= Object.FindObjectsOfType<Image>(true);
                while (imageAt < images.Length && handled < MaxPerFrame && clock.Elapsed.TotalMilliseconds < FrameBudgetMs)
                {
                    LookAt(images[imageAt++]);
                    handled++;
                }
                if (imageAt >= images.Length) { imagesDone = true; images = null; }
                return;
            }
            if (!spritesDone)
            {
                sprites ??= Resources.FindObjectsOfTypeAll<Sprite>();
                while (spriteAt < sprites.Length && handled < MaxPerFrame && clock.Elapsed.TotalMilliseconds < FrameBudgetMs)
                {
                    LookAt(sprites[spriteAt++]);
                    handled++;
                }
                if (spriteAt >= sprites.Length) { spritesDone = true; sprites = null; }
                return;
            }
            Done = true;
            Main.logger_instance?.Msg("Crafting icons: gold " + (Gold != null) + ", bones " + (Bones != null) + ", ember "
                + (Ember != null || EmberBuff != null) + ", memory amber " + (Amber != null));
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Crafting icon search: " + ex.Message);
            images = null;
            sprites = null;
            imagesDone = spritesDone = Done = true; // give up quietly; the windows fall back to their own search
        }
    }

    private static bool Dead(Sprite sprite) => sprite != null && sprite.IsNullOrDestroyed();

    private static void Restart()
    {
        Done = false;
        imagesDone = spritesDone = false;
        imageAt = spriteAt = 0;
        images = null;
        sprites = null;
        Gold = Bones = Ember = EmberBuff = AmberFaction = AmberEcho = null;
        readySince = -1f;
    }

    private static void LookAt(Image image)
    {
        if (image == null || image.sprite == null) return;
        string name = image.gameObject.name;
        if (Gold == null && name == "Gold Coins Icon") Gold = image.sprite;
        else if (Bones == null && name == "Ancient Bones Icon") Bones = image.sprite;
    }

    private static void LookAt(Sprite sprite)
    {
        if (sprite == null) return;
        string name = sprite.name;
        if (Ember == null && name.Equals("SoulEmber_Icon", StringComparison.OrdinalIgnoreCase)) Ember = sprite;
        else if (EmberBuff == null && name.Equals("SoulEmber_Icon_Buff", StringComparison.OrdinalIgnoreCase)) EmberBuff = sprite;
        else if (AmberFaction == null && name.Equals("Faction_TW_MemoryAmber", StringComparison.OrdinalIgnoreCase)) AmberFaction = sprite;
        else if (AmberEcho == null && name.Equals("EchoReward_MemoryAmber", StringComparison.OrdinalIgnoreCase)) AmberEcho = sprite;
    }
}
