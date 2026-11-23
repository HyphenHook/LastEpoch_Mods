using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Heorot;
using LastEpoch_Hud.Scripts.Mods.Character.Heorot;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Character.Volcanic;

internal enum VolcanicBuffKind { Wrath, Hunger, Vessel }

/// <summary>
/// The three buffs of Volcanic Shaman’s Core, registered as native positive statuses (154–156) the same way as Heorot’s.
/// Their flat, chance and resistance stats come from VolcanicStats.csv; the lines no native stat can express are in VolcanicCombat.
/// </summary>
internal static class VolcanicBuffs
{
    private const byte FirstId = 154;
    private static readonly Ailment[] definitions = new Ailment[3];
    private static readonly string[] names = { "Bhuldar’s Anger", "Logi’s Hunger", "Herkir’s Vessel" };
    private static readonly string[] descriptions = new string[3];
    private static readonly string[] icons = { "volcanic_wrath", "volcanic_hunger", "volcanic_vessel" };
    private static readonly HashSet<Sprite> ownedIcons = new();
    private static bool registered;
    private static bool failed;
    private static string status = "Waiting for the ailment catalog.";
    private static List<HeorotStatRow> statRows = new();
    private static bool dataLoaded;

    internal static bool Registered => registered;
    internal static AilmentID Id(VolcanicBuffKind kind) => (AilmentID)(FirstId + (int)kind);

    internal static bool TryKind(int id, out VolcanicBuffKind kind)
    {
        kind = (VolcanicBuffKind)(id - FirstId);
        return id >= FirstId && id < FirstId + 3;
    }

    internal static bool IsActive(Actor actor, VolcanicBuffKind kind) => registered
        && !actor.IsNullOrDestroyed() && !actor.ailmentReceiver.IsNullOrDestroyed()
        && actor.ailmentReceiver.hasAilment(Id(kind));

    internal static Sprite Icon(VolcanicBuffKind kind)
    {
        var icon = OrbArt.Named(icons[(int)kind]);
        if (icon != null) ownedIcons.Add(icon);
        return icon;
    }

    internal static bool OwnsIcon(Sprite sprite) => sprite != null && ownedIcons.Contains(sprite);

    internal static string DescriptionFor(VolcanicBuffKind kind) => descriptions[(int)kind];

    internal static bool TryDescription(Ailment ailment, out string text)
    {
        text = null;
        if (!TryKind((int)ailment.id, out var kind) || descriptions[(int)kind] == null) return false;
        text = descriptions[(int)kind];
        return true;
    }

    private static void LoadData()
    {
        if (dataLoaded) return;
        var errors = new List<string>();
        statRows = HeorotTable.ParseStats(EmbeddedData.Read("VolcanicStats.csv"), errors);
        VolcanicValues.Set(HeorotTable.ParseValues(EmbeddedData.Read("VolcanicValues.csv"), errors));
        foreach (string error in errors) Main.logger_instance?.Warning("Volcanic data: " + error);
        dataLoaded = true;
    }

    private static IEnumerable<HeorotStatRow> RowsFor(VolcanicBuffKind kind) =>
        statRows.Where(r => string.Equals(r.Buff, kind.ToString(), StringComparison.OrdinalIgnoreCase));

    private static string Percent(float fraction) => HeorotBuffs.Number(fraction * 100f);

    // The lines no native stat can express. Their numbers come from VolcanicValues.csv.
    private static string[] CustomLines(VolcanicBuffKind kind) => kind switch
    {
        VolcanicBuffKind.Wrath => new[] { Percent(VolcanicValues.IncreasedFirePerIgnite) + "% increased Fire Damage per stack of Ignite on the target." },
        VolcanicBuffKind.Hunger => new[]
        {
            Percent(VolcanicValues.FireLeech) + "% of Fire damage dealt is leeched as Health.",
            Percent(VolcanicValues.NecroticLeech) + "% of Necrotic damage dealt is leeched as Health.",
        },
        VolcanicBuffKind.Vessel => new[] { Percent(VolcanicValues.IgniteAttackersChance) + "% chance to Ignite attackers for "
            + HeorotBuffs.Number(VolcanicValues.IgniteDamagePerResistance) + " Fire damage per 1% uncapped Fire Resistance." },
        _ => Array.Empty<string>(),
    };

    private static void BuildDescriptions()
    {
        for (int i = 0; i < descriptions.Length; i++)
        {
            var kind = (VolcanicBuffKind)i;
            var lines = new List<string>();
            foreach (var row in RowsFor(kind))
            {
                try { lines.Add(HeorotBuffs.StatLine(row)); }
                catch (Exception ex)
                {
                    Main.logger_instance?.Warning("Volcanic native stat wording: " + ex.Message);
                    lines.Add(HeorotBuffs.PlainLine(row));
                }
            }
            lines.AddRange(CustomLines(kind));
            descriptions[i] = string.Join("\n", lines);
        }
    }

    internal static void Tick()
    {
        // Heorot’s buffs register first, so the status list is only changed one set at a time.
        if (registered || failed || !HeorotBuffs.Registered || Locales.current == Locales.Selected.Unknow) return;
        var list = AilmentList.get();
        if (list.IsNullOrDestroyed() || list.list == null) return;
        try
        {
            foreach (var ailment in list.list)
                if (!ailment.IsNullOrDestroyed() && TryKind((int)ailment.id, out _))
                    throw new InvalidOperationException("A reserved Volcanic status ID is already registered: " + ailment.id);
            var fallback = AilmentList.getAilment(AilmentID.Frenzy);
            LoadData();
            foreach (VolcanicBuffKind kind in Enum.GetValues(typeof(VolcanicBuffKind)))
                if (!RowsFor(kind).Any() && kind != VolcanicBuffKind.Vessel)
                    throw new InvalidOperationException("VolcanicStats.csv has no stats for " + kind + ".");
            BuildDescriptions();
            for (int i = 0; i < definitions.Length; i++)
            {
                var kind = (VolcanicBuffKind)i;
                var ailment = ScriptableObject.CreateInstance<Ailment>();
                ailment.hideFlags = HideFlags.HideAndDontSave;
                ailment.name = "Volcanic_" + kind;
                ailment.id = Id(kind);
                ailment.displayName = names[i];
                ailment.instanceName = "Volcanic_" + kind;
                ailment.description = descriptions[i];
                // Reference unit only. Applications supply their own duration (a worn Core applies it without end).
                ailment.duration = 1f;
                ailment.maxInstances = 1;
                ailment.maxStacksThatApplyBuffs = 1;
                ailment.positive = true;
                ailment.showsInBuffUI = true;
                ailment.topOfScreenDisplayType = Ailment.TopOfScreenDisplayType.Shown;
                ailment.buffScalingType = Ailment.BuffScalingType.Individual;
                if (!fallback.IsNullOrDestroyed()) ailment.iconSoftRef = fallback.iconSoftRef;
                ailment.buffs = new Il2CppSystem.Collections.Generic.List<Stats.Stat>();
                foreach (var row in RowsFor(kind)) ailment.buffs.Add(HeorotBuffs.BuildStat(row));
                definitions[i] = ailment;
            }
            foreach (var definition in definitions) list.list.Add(definition);
            list.forceRefreshDictionary();
            for (int i = 0; i < definitions.Length; i++)
                if (AilmentList.getAilment(Id((VolcanicBuffKind)i))?.Pointer != definitions[i].Pointer)
                    throw new InvalidOperationException("The native Volcanic status lookup failed.");
            registered = true;
            status = "Registered Bhuldar’s Anger, Logi’s Hunger and Herkir’s Vessel as native positive statuses (154–156).";
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
            catch (Exception rollback) { ErrorLog.Report(rollback, "Volcanic registration rollback"); }
            status = "Volcanic registration failed: " + ex.Message;
            ErrorLog.Report(ex, "Volcanic buffs");
        }
    }

    /// <summary>The wearer’s increased effect of this buff (0.2 = 20%), as the game scales the buff’s stats when it is applied.</summary>
    internal static float EffectOf(Actor actor, VolcanicBuffKind kind)
    {
        if (actor.IsNullOrDestroyed() || actor.stats.IsNullOrDestroyed()) return 0f;
        var stats = actor.stats;
        float effect = stats.GetTotalIncreased(SP.IncreasedAilmentEffect, AT.None, 0, 0)
            + stats.GetTotalIncreased(SP.EffectOfAilmentOnYou, AT.None, (byte)Id(kind), 0);
        return float.IsFinite(effect) ? Math.Max(0f, effect) : 0f;
    }

    internal static void Apply(Actor actor, VolcanicBuffKind kind, float duration)
    {
        Tick();
        if (!registered) throw new InvalidOperationException(status);
        if (actor.IsNullOrDestroyed() || actor.ailmentReceiver.IsNullOrDestroyed()) return;
        if (!float.IsFinite(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
        actor.ailmentReceiver.ApplyStackOfAilmentForDuration(definitions[(int)kind], duration, EffectOf(actor, kind), actor, false);
    }

    internal static void Remove(Actor actor, VolcanicBuffKind kind)
    {
        if (!registered || actor.IsNullOrDestroyed() || actor.ailmentReceiver.IsNullOrDestroyed()) return;
        actor.ailmentReceiver.cleanseAilment(Id(kind), false, 1f, false, false, null, 0f, false, null, true);
    }

    // Debug toggles are untimed and never saved.
    internal static void ToggleTest(VolcanicBuffKind kind)
    {
        try
        {
            var character = PlayerFinder.getPlayerData();
            if (character.IsNullOrDestroyed() || !character.IsOffline) return;
            var actor = Refs_Manager.player_actor;
            if (IsActive(actor, kind)) Remove(actor, kind);
            else Apply(actor, kind, float.MaxValue);
        }
        catch (Exception ex) { ErrorLog.Report(ex, "Volcanic test buff"); }
    }

    internal static string Inspect()
    {
        var actor = Refs_Manager.player_actor;
        string result = status;
        for (int i = 0; i < 3; i++) result += "\n" + names[i] + ": " + IsActive(actor, (VolcanicBuffKind)i);
        if (!actor.IsNullOrDestroyed() && !actor.stats.IsNullOrDestroyed())
        {
            var stats = actor.stats;
            result += "\nNative added Fire Damage: " + stats.GetTotalAdded(SP.Damage, AT.Fire, 0, 0, false)
                + "\nNative increased Fire Damage pool: " + stats.GetTotalIncreased(SP.Damage, AT.Fire, 0, 0)
                + "\nNative added Ignite chance: " + stats.GetTotalAdded(SP.AilmentChance, AT.None, (byte)AilmentID.Ignite, 0, false)
                + "\nNative added Shred Fire Resistance chance: " + stats.GetTotalAdded(SP.AilmentChance, AT.None, (byte)AilmentID.FireResShred, 0, false)
                + "\nUncapped Fire Resistance (used by Herkir’s Vessel): " + HeorotCombat.UncappedFireResistance(actor)
                + "\nNative added Fire Resistance: " + stats.GetTotalAdded(SP.FireResistance, AT.None, 0, 0, false)
                + "\nNative added Necrotic Resistance: " + stats.GetTotalAdded(SP.NecroticResistance, AT.None, 0, 0, false);
        }
        return result;
    }

    // Called from HeorotBuffs’ patch of the buff icon, so the game’s method is patched only once.
    internal static void ApplyIcon(BuffUIEntry entry)
    {
        if (!entry.isAilment || !TryKind(entry.id, out var kind)) return;
        entry.content.overrideSprite = Icon(kind);
        if (entry.maxRemainingDuration > 1e20f)
        {
            entry.noFixedDuration = true;
            entry.ignoreRemainingDuration = true;
        }
    }
}
