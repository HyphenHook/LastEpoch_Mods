using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LastEpoch_Hud.Scripts.Mods.Items.Accessories;

namespace LastEpoch_Hud.Scripts.Mods.Items.Jewels;

/// <summary>
/// What equipped Jewels do to the character: the native stats they add, the attribute increases the game would skip, and
/// the inspection text for them. The slots themselves (storage, saving, moving) are shared with the Charm in
/// <c>AccessoryStore</c>.
/// </summary>
internal static class JewelEquipment
{
    internal const byte Type = 44;

    /// <summary>The stat changes worked out for a set of Jewels, ready to apply.</summary>
    internal sealed class Plan
    {
        internal readonly List<Stats.Stat> Modifiers = new();
        internal readonly float[] AttributeIncrease = new float[AttributeProperties.Length];
    }

    // The game applies "increased" to most stats but not to a core attribute, so Jewel increases to Strength, Dexterity,
    // Intelligence, Attunement and Vitality are worked out here and added as a flat amount of that attribute.
    private static readonly SP[] AttributeProperties = { SP.Strength, SP.Dexterity, SP.Intelligence, SP.Attunement, SP.Vitality };
    private static readonly CoreAttribute.Attribute[] AttributeIds =
    {
        CoreAttribute.Attribute.Strength, CoreAttribute.Attribute.Dexterity, CoreAttribute.Attribute.Intelligence,
        CoreAttribute.Attribute.Attunement, CoreAttribute.Attribute.Vitality,
    };
    private static readonly float[] attributeIncrease = new float[5];
    private static readonly int[] attributeBonus = new int[5];
    private static readonly List<Stats.Stat> appliedStats = new();
    private static BaseStats target;

    internal static bool IsJewel(ItemData item) => JewelBaseType.Registered && !item.IsNullOrDestroyed()
        && item.itemType == Type && item.subType < 4
        && (item.rarity <= 6 || HeorotCore.IsAnyCore(item));

    internal static int AppliedStatCount => appliedStats.Count;

    /// <summary>How many Jewels can be equipped. They are slots 1 to 3 of the accessory store, after the Charm.</summary>
    internal const int SlotCount = 3;
    internal const int FirstStoreIndex = 1;
    private static string appliedSignature;

    internal static bool IsRegistered => JewelBaseType.Registered && JewelAffixes.Registered;

    /// <summary>The Jewel in slot 0 to 2 (left to right and top to bottom), or null.</summary>
    internal static ItemDataUnpacked EquippedAt(int slot) =>
        slot >= 0 && slot < SlotCount ? AccessoryStore.At(FirstStoreIndex + slot) : null;

    /// <summary>Gives the character the stats of the equipped Jewels, or takes them back when they changed.</summary>
    internal static void Refresh(BaseStats to)
    {
        var slots = Enumerable.Range(0, SlotCount).Select(EquippedAt).ToArray();
        string signature = to.IsNullOrDestroyed() ? null
            : string.Join("|", slots.Select(x => x == null ? "" : Convert.ToBase64String(x.GetID().ToArray())));
        if (target == to && appliedSignature == signature) return;
        // Resolve all native rolls before touching the active modifiers.
        var plan = to.IsNullOrDestroyed() ? null : Prepare(slots.Select(x => x != null && IsJewel(x) ? x : null));
        appliedSignature = null;
        var previous = target;
        Remove(previous);
        if (!previous.IsNullOrDestroyed()) previous.UpdateStatsNextFrame();
        if (to.IsNullOrDestroyed())
        {
            Clear();
            return;
        }
        Apply(to, plan, ItemContainersManager.Instance.actor, slots);
        appliedSignature = signature;
        to.UpdateStatsNextFrame();
    }

    /// <summary>Works out the native stats and attribute increases of these Jewels (null entries are empty slots).</summary>
    internal static Plan Prepare(IEnumerable<ItemDataUnpacked> jewels)
    {
        var plan = new Plan();
        foreach (var item in jewels)
        {
            if (item == null) continue;
            // addStat may collapse/mutate the Stat passed to it, so keep independent snapshots.
            var native = new Il2CppSystem.Collections.Generic.List<Stats.Stat>();
            JewelStatEffects.AddImplicitStats(item, native);
            if (item.affixes != null)
                foreach (var affix in item.affixes)
                    item.AddAffixValueToStatList(affix, false, ref native);
            foreach (var stat in native)
            {
                var copy = new Stats.Stat(stat);
                int attribute = Array.IndexOf(AttributeProperties, copy.property);
                if (attribute >= 0 && copy.increasedValue != 0)
                {
                    plan.AttributeIncrease[attribute] += copy.increasedValue;
                    copy.increasedValue = 0;
                    if (copy.addedValue == 0) continue;
                }
                plan.Modifiers.Add(copy);
            }
        }
        return plan;
    }

    /// <summary>Takes back everything applied so far from <paramref name="from" /> (which may already be gone).</summary>
    internal static void Remove(BaseStats from)
    {
        if (!from.IsNullOrDestroyed())
        {
            // Pop only after successful removal so an interrupted refresh can retry.
            while (appliedStats.Count > 0)
            {
                int last = appliedStats.Count - 1;
                from.removeStat(appliedStats[last]);
                appliedStats.RemoveAt(last);
            }
            for (int i = 0; i < attributeBonus.Length; i++)
            {
                if (attributeBonus[i] != 0)
                    from.RemoveStatModifier(AttributeProperties[i], attributeBonus[i], BaseStats.ModType.ADDED, (AT)0, 0, 0);
                attributeBonus[i] = 0;
            }
        }
        target = null;
        Array.Clear(attributeIncrease, 0, attributeIncrease.Length);
        appliedStats.Clear();
    }

    /// <summary>Nothing is equipped on a character any more.</summary>
    internal static void Clear() => JewelStatEffects.Clear();

    /// <summary>Gives the character the stats of <paramref name="plan" />.</summary>
    internal static void Apply(BaseStats to, Plan plan, Actor actor, IEnumerable<ItemDataUnpacked> jewelSlots)
    {
        target = to;
        foreach (var stat in plan.Modifiers)
        {
            to.addStat(new Stats.Stat(stat));
            appliedStats.Add(stat);
        }
        Array.Copy(plan.AttributeIncrease, attributeIncrease, attributeIncrease.Length);
        JewelStatEffects.Refresh(actor, jewelSlots);
        if (attributeIncrease.Any(x => x > 0))
        {
            // Read the attributes as they are now, not as they were a moment ago, so the increase is right straight away.
            to.UpdateStats();
            MaintainAttributeBonuses();
        }
    }

    /// <summary>Keeps each attribute's bonus equal to its increase times the attribute without that bonus.</summary>
    internal static void MaintainAttributeBonuses()
    {
        var stats = target;
        if (stats.IsNullOrDestroyed()) return;
        bool changed = false;
        for (int i = 0; i < AttributeProperties.Length; i++)
        {
            int desired = 0;
            if (attributeIncrease[i] > 0)
            {
                float own = stats.GetAttributeValue(AttributeIds[i]) - attributeBonus[i];
                desired = (int)Math.Floor(Math.Max(0f, own) * attributeIncrease[i]);
            }
            if (desired == attributeBonus[i]) continue;
            if (attributeBonus[i] != 0)
                stats.RemoveStatModifier(AttributeProperties[i], attributeBonus[i], BaseStats.ModType.ADDED, (AT)0, 0, 0);
            if (desired != 0)
                stats.AddStatModifier(AttributeProperties[i], desired, BaseStats.ModType.ADDED, (AT)0, 0, 0);
            attributeBonus[i] = desired;
            changed = true;
        }
        if (changed) stats.UpdateStatsNextFrame();
    }

    // What the Jewels hand to the game, and what the character ends up with. For checking implicit and attribute maths.
    internal static string Inspect(Actor actor)
    {
        var text = new System.Text.StringBuilder();
        text.Append("Applied Jewel stats (" + appliedStats.Count + "):");
        foreach (var stat in appliedStats)
            text.Append("\n  " + stat.property + " tags=" + stat.tags + " added=" + stat.addedValue + " increased=" + stat.increasedValue
                + " more=" + (stat.moreValues == null ? "none" : string.Join("/", stat.moreValues.ToArray())));
        text.Append("\n" + JewelStatEffects.Inspect());
        var stats = actor.stats;
        text.Append("\nAttribute increases from Jewels: Str " + attributeIncrease[0] + ", Dex " + attributeIncrease[1] + ", Int " + attributeIncrease[2]
            + ", Att " + attributeIncrease[3] + ", Vit " + attributeIncrease[4] + "; bonus added: " + string.Join("/", attributeBonus));
        text.Append("\nAttributes: Str " + stats.GetAttributeValue(CoreAttribute.Attribute.Strength)
            + ", Dex " + stats.GetAttributeValue(CoreAttribute.Attribute.Dexterity)
            + ", Int " + stats.GetAttributeValue(CoreAttribute.Attribute.Intelligence)
            + ", Att " + stats.GetAttributeValue(CoreAttribute.Attribute.Attunement)
            + ", Vit " + stats.GetAttributeValue(CoreAttribute.Attribute.Vitality));
        text.Append("\nTotals: added All Attributes " + stats.GetTotalAdded(SP.AllAttributes, AT.None, 0, 0, false)
            + ", added Mana " + stats.GetTotalAdded(SP.Mana, AT.None, 0, 0, false) + ", increased Mana " + stats.GetTotalIncreased(SP.Mana, AT.None, 0, 0)
            + ", added Health " + stats.GetTotalAdded(SP.Health, AT.None, 0, 0, false) + ", increased Health " + stats.GetTotalIncreased(SP.Health, AT.None, 0, 0)
            + ", increased Damage " + stats.GetTotalIncreased(SP.Damage, AT.None, 0, 0));
        return text.ToString();
    }
}
