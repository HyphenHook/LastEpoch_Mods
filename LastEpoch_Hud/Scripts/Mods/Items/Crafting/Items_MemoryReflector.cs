using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.Core.Items;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// The Memory Reflector's game side: checks that two items can be paired, lists what item B can give, and moves the
/// affix across. Item B is spent either way. See <see cref="ReflectorMath" /> for the odds and the cost.
/// </summary>
internal static class Items_MemoryReflector
{
    private static readonly Random Rng = new();

    /// <summary>One affix item B could give.</summary>
    public sealed class Candidate
    {
        public ItemAffix Source;
        public int AffixId;
        public int Tier; // displayed, 1..7
        public int Placement; // 0 neither, 1 prefix, 2 suffix
        public bool CanGo; // may it sit on item A?
        public string Why = ""; // when not
    }

    public sealed class Offer
    {
        public ItemData A;
        public ItemData B;
        public bool Legendary;
        public int ACount;
        public int BCount;
        public List<Candidate> Candidates = new();
        public int APrefixes;
        public int ASuffixes;
    }

    /// <summary>The ordinary (not sealed, not primordial) affixes of an item.</summary>
    public static List<ItemAffix> Ordinary(ItemData item)
    {
        var list = new List<ItemAffix>();
        if (item?.affixes == null)
        {
            return list;
        }
        foreach (ItemAffix a in item.affixes.ToArray())
        {
            if (a != null && !a.IsSealed)
            {
                list.Add(a);
            }
        }
        return list;
    }

    private static bool IsLegendary(ItemData item) => item.rarity == 9;

    /// <summary>Why these two items cannot be reflected, or an empty string.</summary>
    public static string WhyNot(ItemData a, ItemData b)
    {
        if (a.IsNullOrDestroyed() || b.IsNullOrDestroyed())
        {
            return "Place an item in each slot.";
        }
        if (a.itemType != b.itemType)
        {
            return "Both items must be the same type of item.";
        }
        if (Items_Recombinator.ClassOf(a) != Items_Recombinator.ClassOf(b))
        {
            return "Both items must be for the same class.";
        }
        if (a.corrupted)
        {
            return "Item A cannot be corrupted.";
        }
        foreach (ItemData item in new[] { a, b })
        {
            if (item.rarity == 7 || item.rarity == 8)
            {
                return "Unique and set items cannot be reflected.";
            }
        }
        if (IsLegendary(a) || IsLegendary(b))
        {
            if (!(IsLegendary(a) && IsLegendary(b)) || a.uniqueID != b.uniqueID)
            {
                return "A legendary item can only be reflected with the same legendary item.";
            }
            Main.logger_instance?.Msg("Memory Reflector legendary: A " + Ordinary(a).Count + " of " + (a.affixes?.Count ?? 0)
                + " affixes, B " + Ordinary(b).Count + " of " + (b.affixes?.Count ?? 0));
            return ReflectorMath.LegendaryWhyNot(Ordinary(a).Count, Ordinary(b).Count);
        }
        return ReflectorMath.WhyNot(Ordinary(a).Count, Ordinary(b).Count);
    }

    /// <summary>What item B can give item A. Only call this once <see cref="WhyNot" /> is empty.</summary>
    public static Offer Describe(ItemData a, ItemData b)
    {
        var offer = new Offer { A = a, B = b, Legendary = IsLegendary(a) };
        List<ItemAffix> own = Ordinary(a);
        offer.ACount = own.Count;
        offer.BCount = Ordinary(b).Count;
        var ownIds = new HashSet<int>(own.Select(x => (int)x.affixId));
        AffixList list = AffixList.get();
        foreach (ItemAffix x in own)
        {
            AffixList.Affix def = list?.GetAffix(x.affixId);
            if (def?.type == AffixList.AffixType.PREFIX)
            {
                offer.APrefixes++;
            }
            else if (def?.type == AffixList.AffixType.SUFFIX)
            {
                offer.ASuffixes++;
            }
        }
        foreach (ItemAffix x in Ordinary(b))
        {
            AffixList.Affix def = list?.GetAffix(x.affixId);
            var candidate = new Candidate
            {
                Source = x,
                AffixId = x.affixId,
                Tier = x.affixTier + 1,
                Placement = def == null ? 0 : def.type == AffixList.AffixType.PREFIX ? 1 : def.type == AffixList.AffixType.SUFFIX ? 2 : 0,
            };
            if (ownIds.Contains(x.affixId))
            {
                candidate.Why = "Item A already has it";
            }
            else if (offer.Legendary)
            {
                candidate.CanGo = true; // a legendary item takes any affix of the same legendary item
            }
            else if (!Items_Recombinator.Legal(def, a, x.affixId))
            {
                candidate.Why = "Cannot go on item A";
            }
            else if ((candidate.Placement == 1 && offer.APrefixes >= AffixLimits.MaxPerSide)
                || (candidate.Placement == 2 && offer.ASuffixes >= AffixLimits.MaxPerSide))
            {
                candidate.Why = candidate.Placement == 1 ? "Item A has three prefixes" : "Item A has three suffixes";
            }
            else
            {
                candidate.CanGo = true;
            }
            offer.Candidates.Add(candidate);
        }
        return offer;
    }

    public static double Chance(Offer offer, Candidate chosen) =>
        offer.Legendary
            ? ReflectorMath.LegendaryChance(offer.ACount, offer.BCount)
            : chosen == null ? 0 : ReflectorMath.Chance(offer.ACount, chosen.Tier);

    public static long Gold(Offer o, Candidate c) =>
        o.Legendary ? ReflectorMath.LegendaryGold(o.ACount, o.BCount) : ReflectorMath.Gold(o.ACount, c?.Tier ?? 4);

    public static int Amber(Offer o, Candidate c) =>
        o.Legendary ? ReflectorMath.LegendaryMemoryAmber(o.ACount, o.BCount) : ReflectorMath.MemoryAmber(o.ACount, c?.Tier ?? 4);

    public static int Bones(Offer o, Candidate c) =>
        o.Legendary ? ReflectorMath.LegendaryBones(o.ACount, o.BCount) : ReflectorMath.Bones(o.ACount, c?.Tier ?? 4);

    /// <summary>
    /// Rolls the reflection. On a success the affix is put on item A (keeping its tier and roll) and A is updated;
    /// item B is the caller's to discard in either case. A legendary item gets a random affix that fits, not a chosen one.
    /// </summary>
    public static bool Reflect(Offer offer, Candidate chosen, out Candidate moved)
    {
        moved = null;
        if (offer.Legendary)
        {
            var open = offer.Candidates.Where(c => c.CanGo).ToList();
            if (open.Count == 0)
            {
                return false;
            }
            chosen = open[Rng.Next(open.Count)];
        }
        if (chosen == null || !chosen.CanGo)
        {
            return false;
        }
        if (Rng.NextDouble() >= Chance(offer, chosen))
        {
            return false;
        }
        ItemAffix s = chosen.Source;
        offer.A.affixes.Add(new ItemAffix((ushort)s.affixId, s.affixTier, s.affixRoll, offer.A.itemType, SealedAffixType.None));
        offer.A.sockets = (byte)offer.A.affixes.Count;
        if (!offer.Legendary)
        {
            // five and six affixes load as rarity 4 and keep every affix
            offer.A.rarity = AffixLimits.LoadableRarity(Ordinary(offer.A).Count);
        }
        offer.A.RefreshIDAndValues();
        moved = chosen;
        return true;
    }
}
