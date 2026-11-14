using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.Core.ForceDrop;
using LastEpoch_Hud.Scripts.Core.Items;
using LastEpoch_Hud.Scripts.ModUI.ForceDrop;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// The recombinator's game side: checks that two items can be merged, lists the affixes they offer, and builds
/// the result. The result is a copy of one of the two items (chosen with equal chance), so its base, implicit
/// rolls, forging potential and its sealed and primordial affixes all stay. Only its ordinary affixes change, and
/// each affix that is carried over keeps its own tier and roll. See <see cref="RecombinatorMath" /> for the odds.
/// </summary>
internal static class Items_Recombinator
{
    private static readonly Random _rng = new();

    public sealed class Offer
    {
        public ItemData A;
        public ItemData B;
        public List<RecombAffix> AffixesA = new();
        public List<RecombAffix> AffixesB = new();
        public bool AnyExalted;
        public int CapacityA;
        public int CapacityB;

        public List<RecombAffix> All => AffixesA.Concat(AffixesB).ToList();
    }

    /// <summary>The class an item is made for (0 when it fits any class).</summary>
    internal static int ClassOf(ItemData item)
    {
        try
        {
            foreach (ItemList.BaseEquipmentItem baseItem in ItemList.get().EquippableItems)
            {
                if ((int)baseItem.type == item.itemType && item.subType < baseItem.subItems.Count)
                {
                    return (int)baseItem.subItems[item.subType].classRequirement;
                }
            }
            return 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>Why these two items cannot be recombined, or an empty string.</summary>
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
        if (ClassOf(a) != ClassOf(b))
        {
            return "Both items must be for the same class.";
        }
        foreach (ItemData item in new[] { a, b })
        {
            if (item.rarity >= 7)
            {
                return "Unique, set and legendary items cannot be recombined.";
            }
            if (item.corrupted)
            {
                return "Corrupted items cannot be recombined.";
            }
        }
        return "";
    }

    /// <summary>The ordinary affixes each item offers, with the game's weights and where each can sit.</summary>
    public static Offer Describe(ItemData a, ItemData b)
    {
        var offer = new Offer { A = a, B = b };
        offer.CapacityA = RecombinatorMath.Capacity(HasRegularSeal(a));
        offer.CapacityB = RecombinatorMath.Capacity(HasRegularSeal(b));
        List(a, b, 0, offer.AffixesA);
        List(b, a, 1, offer.AffixesB);
        offer.AnyExalted = offer.All.Any(x => x.IsExalted);
        return offer;
    }

    private static bool HasRegularSeal(ItemData item)
    {
        if (item?.affixes == null)
        {
            return false;
        }
        foreach (ItemAffix x in item.affixes)
        {
            if (x != null && x.IsSealedRegular)
            {
                return true;
            }
        }
        return false;
    }

    private static void List(ItemData own, ItemData other, int source, List<RecombAffix> into)
    {
        if (own?.affixes == null)
        {
            return;
        }
        AffixList list = AffixList.get();
        foreach (ItemAffix x in own.affixes)
        {
            if (x == null || x.IsSealed)
            {
                continue; // sealed and primordial affixes stay with their own base
            }
            AffixList.Affix def = list?.GetAffix(x.affixId);
            bool legalOnOwn = Legal(def, own, x.affixId);
            bool legalOnOther = Legal(def, other, x.affixId);
            into.Add(new RecombAffix
            {
                AffixId = x.affixId,
                DisplayedTier = x.affixTier + 1,
                Weight = def == null ? 1 : Math.Max(0.01, def.weighting),
                Placement = def == null ? 0 : def.type == AffixList.AffixType.PREFIX ? 1 : def.type == AffixList.AffixType.SUFFIX ? 2 : 0,
                Source = source,
                LegalOnA = source == 0 ? legalOnOwn : legalOnOther,
                LegalOnB = source == 1 ? legalOnOwn : legalOnOther,
            });
        }
    }

    /// <summary>Can this affix sit on that item's base, alongside the affixes it would have?</summary>
    internal static bool Legal(AffixList.Affix def, ItemData onto, int affixId)
    {
        if (def == null)
        {
            return false;
        }
        try
        {
            var others = new List<int>();
            var legal = new ForceDropLegalAffixes(onto.itemType, onto.subType, 2, others);
            return legal.OrdinaryReason(def, false, false).Length == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Recombines the two items. In the deterministic mode <paramref name="selected" /> are the affixes wanted: they
    /// are kept when the merge succeeds, and otherwise the result is an indeterministic one. Returns the new item.
    /// </summary>
    public static ItemData Recombine(Offer offer, bool deterministic, IReadOnlyList<RecombAffix> selected, out bool exact, out string note, bool primordial = false)
    {
        exact = false;
        note = "";
        double Rng() => _rng.NextDouble();

        int baseIndex = RecombinatorMath.PickBase(Rng);
        ItemData own = baseIndex == 0 ? offer.A : offer.B;
        int capacity = baseIndex == 0 ? offer.CapacityA : offer.CapacityB;
        var pool = offer.All.Where(x => x.LegalOn(baseIndex)).ToList();

        // The result holds the average number of affixes of the two items.
        // The average of the two items. The indeterministic mode usually lands on it and sometimes strays by one;
        // the deterministic mode always keeps it.
        int count = RecombinatorMath.ResultCount(offer.AffixesA.Count, offer.AffixesB.Count, pool.Count, Rng, capacity, varied: !deterministic);

        List<RecombAffix> chosen;
        if (deterministic && selected != null && selected.Count > 0
            && RecombinatorMath.Validate(selected, capacity).Length == 0
            && selected.All(x => x.LegalOn(baseIndex))
            && Rng() < RecombinatorMath.SurvivalChance(selected, primordial))
        {
            chosen = selected.ToList();
            exact = true;
            note = "The recombination succeeded.";
            // Slots the pick leaves open are filled at random from what the items offer.
            var taken = new HashSet<int>(chosen.Select(x => x.AffixId));
            var rest = pool.Where(x => !taken.Contains(x.AffixId)).ToList();
            chosen.AddRange(RecombinatorMath.Draw(
                rest,
                Math.Min(Math.Max(0, count - chosen.Count), capacity - chosen.Count),
                Rng,
                chosen.Count(x => x.Placement == 1),
                chosen.Count(x => x.Placement == 2)));
        }
        else
        {
            chosen = RecombinatorMath.Draw(pool, count, Rng);
            note = deterministic ? "The recombination failed: the result is a random mix." : "Recombined.";
        }

        // The result is a copy of the winning base, so its implicit rolls and sealed affixes come along.
        ItemData result = Clone(own);
        if (result.IsNullOrDestroyed())
        {
            note = "Could not copy the item.";
            return null;
        }
        var drop = new List<ItemAffix>();
        foreach (ItemAffix x in result.affixes)
        {
            if (x != null && !x.IsSealed)
            {
                drop.Add(x);
            }
        }
        foreach (ItemAffix x in drop)
        {
            result.affixes.Remove(x);
        }
        foreach (RecombAffix pick in chosen)
        {
            ItemData from = pick.Source == 0 ? offer.A : offer.B;
            ItemAffix source = from.affixes.ToArray().FirstOrDefault(x => x != null && !x.IsSealed && x.affixId == pick.AffixId);
            if (source == null)
            {
                continue;
            }
            // The affix keeps its own tier and roll.
            result.affixes.Add(new ItemAffix((ushort)source.affixId, source.affixTier, source.affixRoll, result.itemType, SealedAffixType.None));
        }
        int ordinary = result.affixes.ToArray().Count(x => x != null && !x.IsSealed);
        result.rarity = AffixLimits.LoadableRarity(ordinary); // five and six affixes load as rarity 4 and keep every affix
        result.sockets = (byte)result.affixes.Count;
        result.RefreshIDAndValues();
        return result;
    }

    /// <summary>A full copy of an item, made by packing it and unpacking it again.</summary>
    private static ItemData Clone(ItemData item)
    {
        var id = item.id;
        if (id == null)
        {
            return null;
        }
        var bytes = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>(id.Length);
        for (int i = 0; i < id.Length; i++)
        {
            bytes[i] = id[i];
        }
        var copy = new ItemDataUnpacked();
        copy.RebuildFromID(bytes);
        copy.RefreshIDAndValues();
        return copy.TryCast<ItemData>();
    }
}
