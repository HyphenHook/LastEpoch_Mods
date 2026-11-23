using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.UI.Tooltips;
using LastEpoch_Hud.Scripts.Core.Heorot;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Character.Heorot;

internal enum HeorotBuffKind { Power, Energy, Defence }

internal static class HeorotBuffs
{
    private const byte FirstId = 151;
    private static readonly Ailment[] definitions = new Ailment[3];
    private static readonly string[] names = { "Heorot’s Power", "Heorot’s Energy", "Heorot’s Defence" };
    private static readonly string[] descriptions = new string[3];
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

    // The native wording for a tagged Damage stat can come back as plain "Increased Damage".
    // Say which damage it is when the stat is limited to one kind.
    private static string EnsureTagged(string text, AT tags)
    {
        if (tags == AT.None || text.IndexOf(tags.ToString(), StringComparison.OrdinalIgnoreCase) >= 0) return text;
        return System.Text.RegularExpressions.Regex.Replace(text, "(?i)(increased) damage", "$1 " + tags + " Damage");
    }

    // The numbers and stats of every buff come from HeorotStats.csv and HeorotValues.csv, embedded at compile time.
    private static List<HeorotStatRow> statRows = new();
    private static bool dataLoaded;

    private static void LoadData()
    {
        if (dataLoaded) return;
        var errors = new List<string>();
        statRows = HeorotTable.ParseStats(EmbeddedData.Read("HeorotStats.csv"), errors);
        HeorotValues.Set(HeorotTable.ParseValues(EmbeddedData.Read("HeorotValues.csv"), errors));
        foreach (string error in errors) Main.logger_instance?.Warning("Heorot data: " + error);
        dataLoaded = true;
    }

    private static IEnumerable<HeorotStatRow> RowsFor(HeorotBuffKind kind) =>
        statRows.Where(r => string.Equals(r.Buff, kind.ToString(), StringComparison.OrdinalIgnoreCase));

    private static AT TagsOf(HeorotStatRow row) =>
        row.Tags.Length == 0 ? AT.None : (AT)Enum.Parse(typeof(AT), row.Tags, true);

    internal static Stats.Stat BuildStat(HeorotStatRow row)
    {
        var tags = TagsOf(row);
        if (row.Kind == "AilmentChance")
            return Stats.AilmentChanceStat((AilmentID)Enum.Parse(typeof(AilmentID), row.Property, true), tags, row.Value, 0);
        var property = (SP)Enum.Parse(typeof(SP), row.Property, true);
        return row.Kind == "Increased"
            ? Stats.IncreasedStat(property, tags, row.Value, 0, 0)
            : Stats.AddedStat(property, tags, row.Value, 0, 0);
    }

    internal static string Number(float value) =>
        value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The ailment as the game calls it on screen: ColdResShred is "Shred Cold Resistance".</summary>
    private static string AilmentName(string ailment)
    {
        const string resistanceShred = "ResShred";
        if (!ailment.EndsWith(resistanceShred, StringComparison.OrdinalIgnoreCase)) return ailment;
        return "Shred " + ailment.Substring(0, ailment.Length - resistanceShred.Length) + " Resistance";
    }

    internal static string StatLine(HeorotStatRow row)
    {
        var tags = TagsOf(row);
        // The game's wording says only "Ailment" for a chance to inflict a particular ailment, so name it here.
        if (row.Kind == "AilmentChance")
            return "+" + Number(row.Value * 100f) + "% chance to inflict " + AilmentName(row.Property) + " on hit";
        var property = (SP)Enum.Parse(typeof(SP), row.Property, true);
        // ...and it leaves the kind of damage out of an added Damage stat, so say which damage it is.
        if (row.Kind == "Added" && property == SP.Damage && tags != AT.None)
            return "+" + Number(row.Value) + " " + tags + " Damage";
        string line = NativeStatLine(property, row.Kind == "Increased" ? BaseStats.ModType.INCREASED : BaseStats.ModType.ADDED, row.Value, tags);
        return row.Kind == "Increased" && property == SP.Damage ? EnsureTagged(line, tags) : line;
    }

    // Plain wording for when the game's own stat wording is not available.
    internal static string PlainLine(HeorotStatRow row) =>
        (row.Kind == "Increased" || row.Kind == "AilmentChance" ? (row.Value * 100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%"
            : row.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))
        + " " + (row.Kind == "AilmentChance" ? AilmentName(row.Property) + " chance" : row.Property) + (row.Tags is "" or "None" ? "" : " (" + row.Tags + ")");

    // The lines no native stat can express. Their numbers come from HeorotValues.csv.
    private static string CustomLine(HeorotBuffKind kind) => kind switch
    {
        HeorotBuffKind.Power => (HeorotRules.IncreasedColdPerChill * 100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            + "% increased Cold Damage per stack of Chill on the target.",
        HeorotBuffKind.Defence => "You can inflict " + HeorotRules.AdditionalChillStacks
            + (HeorotRules.AdditionalChillStacks == 1 ? " additional stack" : " additional stacks") + " of Chill.",
        _ => null,
    };

    private static void BuildDescriptions()
    {
        for (int i = 0; i < descriptions.Length; i++)
        {
            var kind = (HeorotBuffKind)i;
            var lines = new List<string>();
            foreach (var row in RowsFor(kind))
            {
                try { lines.Add(StatLine(row)); }
                catch (Exception ex)
                {
                    Main.logger_instance?.Warning("Heorot native stat wording: " + ex.Message);
                    lines.Add(PlainLine(row));
                }
            }
            string custom = CustomLine(kind);
            if (custom != null) lines.Add(custom);
            descriptions[i] = string.Join("\n", lines);
        }
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
            LoadData();
            if (!RowsFor(HeorotBuffKind.Power).Any() || !RowsFor(HeorotBuffKind.Energy).Any() || !RowsFor(HeorotBuffKind.Defence).Any())
                throw new InvalidOperationException("HeorotStats.csv has no stats for one of the buffs.");
            BuildDescriptions();
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
                foreach (var row in RowsFor(kind)) ailment.buffs.Add(BuildStat(row));
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

    /// <summary>
    /// The wearer's increased effect of this buff (0.2 = 20%): the game's increased effect of ailments, plus its increased
    /// effect of this particular status on you. The game scales the buff's stats by it when the buff is applied.
    /// </summary>
    internal static float EffectOf(Actor actor, HeorotBuffKind kind)
    {
        if (actor.IsNullOrDestroyed() || actor.stats.IsNullOrDestroyed()) return 0f;
        var stats = actor.stats;
        float effect = stats.GetTotalIncreased(SP.IncreasedAilmentEffect, AT.None, 0, 0)
            + stats.GetTotalIncreased(SP.EffectOfAilmentOnYou, AT.None, (byte)Id(kind), 0);
        return float.IsFinite(effect) ? Math.Max(0f, effect) : 0f;
    }

    internal static void Apply(Actor actor, HeorotBuffKind kind, float duration)
    {
        Tick();
        if (!registered) throw new InvalidOperationException(status);
        if (actor.IsNullOrDestroyed() || actor.ailmentReceiver.IsNullOrDestroyed()) return;
        if (!float.IsFinite(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
        actor.ailmentReceiver.ApplyStackOfAilmentForDuration(definitions[(int)kind], duration, EffectOf(actor, kind), actor, false);
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
            result += "\nIncreased effect of buffs (Power/Energy/Defence): " + EffectOf(actor, HeorotBuffKind.Power) + "/"
                + EffectOf(actor, HeorotBuffKind.Energy) + "/" + EffectOf(actor, HeorotBuffKind.Defence);
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
            if (!TryKind((int)__instance.id, out var kind))
            {
                if (!Volcanic.VolcanicBuffs.TryDescription(__instance, out string volcanic)) return true;
                __result = volcanic;
                return false;
            }
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
                if (__instance.isAilment && Volcanic.VolcanicBuffs.TryKind(__instance.id, out _))
                {
                    Volcanic.VolcanicBuffs.ApplyIcon(__instance);
                    return;
                }
                if (ownedIcons.Contains(__instance.content.overrideSprite)
                    || Volcanic.VolcanicBuffs.OwnsIcon(__instance.content.overrideSprite)) __instance.content.overrideSprite = null;
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
            if (!__instance.isAilment || (!TryKind(__instance.id, out _) && !Volcanic.VolcanicBuffs.TryKind(__instance.id, out _)) || __0 <= 1e20f) return;
            __instance.noFixedDuration = true;
            __instance.ignoreRemainingDuration = true;
            __instance.timeOverlay?.SetActive(false);
        }
    }
}
