using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.UI.Tooltips;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Character.Heorot;

internal enum HeorotBuffKind { Power, Energy, Defence }

internal static class HeorotBuffs
{
    private const byte FirstId = 151;
    private static readonly Ailment[] definitions = new Ailment[3];
    private static readonly string[] names = { "Heorot’s Power", "Heorot’s Energy", "Heorot’s Defence" };
    private static readonly string[] descriptions =
    {
        "Adds 120 Cold Damage.\n25% increased Cold Damage per stack of Chill on the target.",
        "+120% chance to inflict Chill on hit.\n+35% chance to inflict Frostbite on hit.\n75% increased Cold Damage.",
        "+120% chance to Chill attackers when hit.\n+65% Cold Resistance.\nYou can inflict 2 additional stacks of Chill.",
    };
    private static readonly string[] icons = { "heorot_power", "heorot_energy", "heorot_defence" };
    private static bool registered;
    private static readonly HashSet<Sprite> ownedIcons = new();
    private static bool failed;
    private static string status = "Waiting for the ailment catalog.";
    internal static bool Registered => registered;
    internal static AilmentID Id(HeorotBuffKind kind) => (AilmentID)(FirstId + (int)kind);
    internal static bool TryKind(int id, out HeorotBuffKind kind)
    {
        kind = (HeorotBuffKind)(id - FirstId);
        return id >= FirstId && id < FirstId + 3;
    }
    internal static bool IsActive(Actor actor, HeorotBuffKind kind) => registered
        && !actor.IsNullOrDestroyed() && !actor.ailmentReceiver.IsNullOrDestroyed()
        && actor.ailmentReceiver.hasAilment(Id(kind));
    internal static Sprite Icon(HeorotBuffKind kind)
    {
        var icon = OrbArt.Named(icons[(int)kind]);
        if (icon != null) ownedIcons.Add(icon);
        return icon;
    }
    internal static string DescriptionFor(HeorotBuffKind kind) => descriptions[(int)kind];

    private static string NativeStatLine(SP property, BaseStats.ModType type, float value, AT tags = AT.None, byte specialTag = 0)
    {
        var info = PropertyList.get().GetPropertyInfo(property, tags, specialTag);
        if (info.IsNullOrDestroyed()) throw new InvalidOperationException("Native stat wording is not loaded.");
        var modifier = new ItemTooltipPropertyModifierInfo(info.getPropertyName(specialTag), info,
            property, tags, specialTag, 0, type, value);
        string text = modifier.GetModifierString(false);
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Native stat wording is empty.");
        return System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
    }

    private static void UseNativeWording()
    {
        try
        {
            descriptions[0] = NativeStatLine(SP.Damage, BaseStats.ModType.ADDED, 120f, AT.Cold)
                + "\n25% increased Cold Damage per stack of Chill on the target.";
            descriptions[1] = NativeStatLine(SP.AilmentChance, BaseStats.ModType.ADDED, 1.2f, AT.None, (byte)AilmentID.Chill)
                + "\n" + NativeStatLine(SP.AilmentChance, BaseStats.ModType.ADDED, .35f, AT.None, (byte)AilmentID.Frostbite)
                + "\n" + NativeStatLine(SP.Damage, BaseStats.ModType.INCREASED, .75f, AT.Cold);
            descriptions[2] = NativeStatLine(SP.ChillRetaliationChance, BaseStats.ModType.ADDED, 1.2f)
                + "\n" + NativeStatLine(SP.ColdResistance, BaseStats.ModType.ADDED, .65f)
                + "\nYou can inflict 2 additional stacks of Chill.";
        }
        catch (Exception ex) { Main.logger_instance?.Warning("Heorot native stat wording: " + ex.Message); }
    }

    internal static void Tick()
    {
        if (registered || failed || Locales.current == Locales.Selected.Unknow) return;
        var list = AilmentList.get();
        if (list.IsNullOrDestroyed() || list.list == null) return;
        try
        {
            foreach (var ailment in list.list)
                if (!ailment.IsNullOrDestroyed() && TryKind((int)ailment.id, out _))
                    throw new InvalidOperationException("A reserved Heorot status ID is already registered: " + ailment.id);
            var fallback = AilmentList.getAilment(AilmentID.Frenzy);
            UseNativeWording();
            for (int i = 0; i < definitions.Length; i++)
            {
                var kind = (HeorotBuffKind)i;
                var ailment = ScriptableObject.CreateInstance<Ailment>();
                ailment.hideFlags = HideFlags.HideAndDontSave;
                ailment.name = "Heorot_" + kind;
                ailment.id = Id(kind);
                ailment.displayName = names[i];
                ailment.instanceName = "Heorot_" + kind;
                ailment.description = descriptions[i];
                // Reference unit only. Applications must supply their own duration;
                // the final gameplay duration and acquisition source are undecided.
                ailment.duration = 1f;
                ailment.maxInstances = 1;
                ailment.maxStacksThatApplyBuffs = 1;
                ailment.positive = true;
                ailment.showsInBuffUI = true;
                ailment.topOfScreenDisplayType = Ailment.TopOfScreenDisplayType.Shown;
                ailment.buffScalingType = Ailment.BuffScalingType.Individual;
                if (!fallback.IsNullOrDestroyed()) ailment.iconSoftRef = fallback.iconSoftRef;
                ailment.buffs = new Il2CppSystem.Collections.Generic.List<Stats.Stat>();
                switch (kind)
                {
                    case HeorotBuffKind.Power:
                        ailment.buffs.Add(Stats.AddedStat(SP.Damage, AT.Cold, 120f, 0, 0));
                        break;
                    case HeorotBuffKind.Energy:
                        ailment.buffs.Add(Stats.AilmentChanceStat(AilmentID.Chill, AT.None, 1.2f, 0));
                        ailment.buffs.Add(Stats.AilmentChanceStat(AilmentID.Frostbite, AT.None, .35f, 0));
                        ailment.buffs.Add(Stats.IncreasedStat(SP.Damage, AT.Cold, .75f, 0, 0));
                        break;
                    case HeorotBuffKind.Defence:
                        ailment.buffs.Add(Stats.AddedStat(SP.ChillRetaliationChance, AT.None, 1.2f, 0, 0));
                        ailment.buffs.Add(Stats.AddedStat(SP.ColdResistance, AT.None, .65f, 0, 0));
                        break;
                }
                definitions[i] = ailment;
            }
            foreach (var definition in definitions) list.list.Add(definition);
            list.forceRefreshDictionary();
            for (int i = 0; i < definitions.Length; i++)
                if (AilmentList.getAilment(Id((HeorotBuffKind)i))?.Pointer != definitions[i].Pointer)
                    throw new InvalidOperationException("The native Heorot status lookup failed.");
            registered = true;
            status = "Registered Heorot’s Power, Energy, and Defence as native positive statuses (151–153).";
            Main.logger_instance?.Msg(status);
        }
        catch (Exception ex)
        {
            failed = true;
            try
            {
                foreach (var definition in definitions)
                    if (!definition.IsNullOrDestroyed()) list.list.Remove(definition);
                list.forceRefreshDictionary();
            }
            catch (Exception rollback) { ErrorLog.Report(rollback, "Heorot registration rollback"); }
            status = "Heorot registration failed: " + ex.Message;
            ErrorLog.Report(ex, "Heorot buffs");
        }
    }

    internal static void Apply(Actor actor, HeorotBuffKind kind, float duration)
    {
        Tick();
        if (!registered) throw new InvalidOperationException(status);
        if (actor.IsNullOrDestroyed() || actor.ailmentReceiver.IsNullOrDestroyed()) return;
        if (!float.IsFinite(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
        actor.ailmentReceiver.ApplyStackOfAilmentForDuration(definitions[(int)kind], duration, 0f, actor, false);
    }

    internal static void Remove(Actor actor, HeorotBuffKind kind)
    {
        if (!registered || actor.IsNullOrDestroyed() || actor.ailmentReceiver.IsNullOrDestroyed()) return;
        actor.ailmentReceiver.cleanseAilment(Id(kind), false, 1f, false, false, null, 0f, false, null, true);
    }

    // Debug toggles are explicitly untimed and never saved. Gameplay duration/source stays configurable.
    internal static void ToggleTest(HeorotBuffKind kind)
    {
        try
        {
            var character = PlayerFinder.getPlayerData();
            if (character.IsNullOrDestroyed() || !character.IsOffline) return;
            var actor = Refs_Manager.player_actor;
            if (IsActive(actor, kind)) Remove(actor, kind);
            else Apply(actor, kind, float.MaxValue);
        }
        catch (Exception ex) { ErrorLog.Report(ex, "Heorot test buff"); }
    }

    internal static string Inspect()
    {
        var actor = Refs_Manager.player_actor;
        string result = status;
        for (int i = 0; i < 3; i++) result += "\n" + names[i] + ": " + IsActive(actor, (HeorotBuffKind)i);
        if (!actor.IsNullOrDestroyed() && !actor.stats.IsNullOrDestroyed())
        {
            var stats = actor.stats;
            result += "\nNative added Cold Damage: " + stats.GetTotalAdded(SP.Damage, AT.Cold, 0, 0, false)
                + "\nNative increased Cold Damage pool: " + stats.GetTotalIncreased(SP.Damage, AT.Cold, 0, 0)
                + "\nNative added Chill chance: " + stats.GetTotalAdded(SP.AilmentChance, AT.None, (byte)AilmentID.Chill, 0, false)
                + "\nNative added Frostbite chance: " + stats.GetTotalAdded(SP.AilmentChance, AT.None, (byte)AilmentID.Frostbite, 0, false)
                + "\nNative added Chill retaliation chance: " + stats.GetTotalAdded(SP.ChillRetaliationChance, AT.None, 0, 0, false)
                + "\nNative added Cold Resistance: " + stats.GetTotalAdded(SP.ColdResistance, AT.None, 0, 0, false);
        }
        return result + "\nDuration/acquisition source: not finalized. Chill chance grants +120 percentage points, additive with other chance sources.";
    }

    [HarmonyPatch(typeof(Ailment), nameof(Ailment.getDescription))]
    private static class Description
    {
        [HarmonyPrefix]
        private static bool Prefix(Ailment __instance, ref string __result)
        {
            if (!TryKind((int)__instance.id, out var kind)) return true;
            __result = descriptions[(int)kind];
            return false;
        }
    }

    [HarmonyPatch(typeof(BuffUIEntry), nameof(BuffUIEntry.initialise))]
    private static class BuffIcon
    {
        [HarmonyPostfix]
        private static void Postfix(BuffUIEntry __instance)
        {
            if (__instance.content.IsNullOrDestroyed()) return;
            if (!__instance.isAilment || !TryKind(__instance.id, out var kind))
            {
                if (ownedIcons.Contains(__instance.content.overrideSprite)) __instance.content.overrideSprite = null;
                return;
            }
            __instance.content.overrideSprite = Icon(kind);
            if (__instance.maxRemainingDuration > 1e20f)
            {
                __instance.noFixedDuration = true;
                __instance.ignoreRemainingDuration = true;
            }
        }
    }

    [HarmonyPatch(typeof(BuffUIEntry), nameof(BuffUIEntry.AddStackGroup))]
    private static class UntimedTestDisplay
    {
        [HarmonyPostfix]
        private static void Postfix(BuffUIEntry __instance, float __0)
        {
            if (!__instance.isAilment || !TryKind(__instance.id, out _) || __0 <= 1e20f) return;
            __instance.noFixedDuration = true;
            __instance.ignoreRemainingDuration = true;
            __instance.timeOverlay?.SetActive(false);
        }
    }
}
