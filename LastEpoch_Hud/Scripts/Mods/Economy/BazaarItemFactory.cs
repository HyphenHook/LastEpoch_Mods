using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Items;

namespace LastEpoch_Hud.Scripts.Mods.Economy;

/// <summary>
/// The market's supply: what a population of 100,000 active players would be listing, as streams.
/// Players farm monoliths for T6/T7 gear, farm legendary-potential uniques, farm bosses for boss-only
/// uniques, get lucky, craft finished items to sell, sell clean bases, and sell ordinary drops. Some of it
/// is corrupted. Luck and crafting are modelled as selection: a lucky or skilled seller's item is the best
/// of many attempts, which is exactly why those items are rare and expensive.
/// </summary>
internal static class BazaarItemFactory
{
    public enum Kind
    {
        Common, // ordinary drops
        Monolith, // exalted gear from monolith farming
        LpUnique, // legendary-potential uniques from farming
        BossUnique, // boss-only uniques
        Jackpot, // very lucky finds
        FinalProduct, // a crafter's finished item
        CleanBase, // a base with room to craft
        Crafted, // crafted until the forging potential is spent
        Creation, // a near-perfect item that had a Rune of Creation used on it, so a twin exists
        Primordial, // a T7 seed evolved into a sealed T8 primordial affix (fallback when the full build fails)
        SealedGear, // a crafter sealed one affix and carried on
        Experimental, // gloves, boots or belts with an experimental affix
        PrimordialSix, // four affixes, a regular seal and a sealed T8: the six-affix endgame item
        Personal, // campaign personal item: cheap levelling gear, no forging potential
        WeaverIdol, // small generic idol with a weaver affix
        HereticalIdol, // enchanted class idol
        WeaversWill, // a unique that levels with Weaver's Will instead of legendary potential
        FpBase, // a magic or rare item with a lot of forging potential left, kept for crafting
        QuadExalted, // four T6/T7 affixes, no primordial: almost never seen
        SixAffix, // five or six ordinary affixes from a crafter, with or without a seal, a primordial affix or corruption
        Legendary, // a unique that already spent its legendary potential on extra affixes
    }

    public sealed class Rolled
    {
        public ItemDataUnpacked Item;
        public Kind Kind;
        public bool Corrupted;
        public bool Twin; // a Rune of Creation was used: an identical copy is listed too
        public string Note = "";
    }

    // Share of listings from each kind of seller. Buyers-only players list nothing, so they do not appear.
    private static readonly (Kind Kind, double Weight)[] Mix =
    {
        (Kind.Common, 8),
        (Kind.Monolith, 36),
        (Kind.LpUnique, 14),
        (Kind.BossUnique, 9),
        (Kind.Jackpot, 3),
        (Kind.FinalProduct, 13),
        (Kind.Crafted, 10),
        (Kind.Creation, 3.5),
        (Kind.PrimordialSix, 3),
        (Kind.SealedGear, 11),
        (Kind.Experimental, 7),
        (Kind.Personal, 2),
        (Kind.WeaversWill, 6),
        (Kind.Legendary, 8),
        (Kind.QuadExalted, 0.15),
        (Kind.SixAffix, 3.2),
        (Kind.FpBase, 4), // scarce: most players craft their own
        (Kind.WeaverIdol, 30),
        (Kind.HereticalIdol, 30),
    };

    private const double CorruptedChance = 0.10; // of non-unique listings
    private const double CorruptedUniqueChance = 0.05;
    private const int FinalProductAttempts = 12;
    private const int JackpotAttempts = 25;
    private const int MonolithItemLevel = 100;

    private static readonly Dictionary<Kind, int> _failures = new();
    private static readonly Random _balanceRng = new();

    // Which kinds of seller list on which stall. Idols and altars have none of the gear-only streams, and
    // gear stalls have none of the idol ones; experimental affixes exist only on belts, boots and gloves.
    private static bool Applies(Kind kind, int stall)
    {
        bool idolStall = stall >= 23 && stall <= 28;
        bool altarStall = stall == 29;
        switch (kind)
        {
            case Kind.Common:
                return true;
            case Kind.WeaverIdol:
                return stall == 23; // the generic idol stall holds the small generic idols
            case Kind.HereticalIdol:
                return stall >= 24 && stall <= 28;
            case Kind.Experimental:
                return stall == 2 || stall == 3 || stall == 4; // belt, boots, gloves
            default:
                return !idolStall && !altarStall;
        }
    }

    public static Kind PickKind(Random rng, int stall)
    {
        double total = Mix.Where(m => Applies(m.Kind, stall)).Sum(m => m.Weight);
        double r = rng.NextDouble() * total;
        foreach (var (kind, weight) in Mix)
        {
            if (!Applies(kind, stall))
            {
                continue;
            }
            r -= weight;
            if (r <= 0)
            {
                return kind;
            }
        }
        return Kind.Common;
    }

    /// <summary>
    /// Real gear mixes prefixes and suffixes. An item whose ordinary affixes all (or mostly) sit on one side is
    /// rare, so most of those are turned away: a balanced item always passes, one side over by one passes
    /// seldom, and one side over by more almost never.
    /// </summary>
    private static bool Balanced(ItemDataUnpacked item, Random rng)
    {
        if (item?.affixes == null || item.rarity >= 7 || item.itemType >= 25)
        {
            return true; // uniques keep their own affixes; idols and altars have no prefix or suffix
        }
        int prefixes = 0, suffixes = 0;
        foreach (ItemAffix a in item.affixes)
        {
            if (a == null || a.IsSealed)
            {
                continue;
            }
            if (a.affixType == AffixList.AffixType.PREFIX)
            {
                prefixes++;
            }
            else if (a.affixType == AffixList.AffixType.SUFFIX)
            {
                suffixes++;
            }
        }
        int n = prefixes + suffixes;
        if (n < 3)
        {
            return true;
        }
        int excess = Math.Max(prefixes, suffixes) - (n + 1) / 2;
        double accept = excess <= 0 ? 1.0 : excess == 1 ? 0.15 : 0.02;
        return rng.NextDouble() < accept;
    }

    /// <summary>At least one T6 or T7 affix: that is what makes gear exalted.</summary>
    private static bool IsExalted(ItemDataUnpacked item)
    {
        if (item?.affixes == null)
        {
            return false;
        }
        foreach (ItemAffix a in item.affixes)
        {
            if (a != null && a.affixTier >= 5)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsCustom(Kind kind) =>
        kind == Kind.SealedGear || kind == Kind.Experimental || kind == Kind.PrimordialSix
        || kind == Kind.Personal || kind == Kind.QuadExalted || kind == Kind.SixAffix || kind == Kind.WeaverIdol || kind == Kind.HereticalIdol;

    public static Rolled Roll(GenerateItems gen, int stall, int[] stallTypes, int ilvl, Random rng)
    {
        Kind kind = PickKind(rng, stall);
        try
        {
            string customNote = "";
            ItemDataUnpacked item = null;
            if (IsCustom(kind))
            {
                BazaarCustomItems.Kind custom = (BazaarCustomItems.Kind)Enum.Parse(typeof(BazaarCustomItems.Kind), kind.ToString());
                item = BazaarCustomItems.Build(custom, stall, stallTypes, rng, out customNote);
                if (item == null && kind == Kind.PrimordialSix)
                {
                    kind = Kind.Primordial; // fall back to evolving a generated item
                }
            }
            if (item == null && !IsCustom(kind))
            {
                item = Make(gen, kind, stall, stallTypes, ilvl, rng);
            }
            if (item == null)
            {
                Fail(kind, "returned nothing");
                return null;
            }
            if (!Balanced(item, rng))
            {
                return null; // lopsided in prefixes and suffixes: try another
            }

            // Gear that is meant to be exalted must hold a T6 or T7 affix, or it is not exalted.
            bool needsExalted = kind == Kind.Monolith || kind == Kind.Jackpot || kind == Kind.FinalProduct
                || kind == Kind.Creation || kind == Kind.Crafted || kind == Kind.Primordial;
            if (needsExalted && !IsExalted(item))
            {
                Fail(kind, "not exalted");
                return null;
            }

            var rolled = new Rolled { Item = item, Kind = kind, Note = customNote };

            // Sets never carry legendary potential.
            if (item.rarity == 8)
            {
                item.legendaryPotential = 0;
            }

            // Season 5 forging potential kinds: Ice may be kept when spent, Blood raises the crit chance.
            if (item.forgingPotential > 0 && item.rarity < 7 && rng.NextDouble() < 0.16)
            {
                item.forgingPotentialType = rng.NextDouble() < 0.5 ? ForgingPotentialType.Ice : ForgingPotentialType.Blood;
            }

            switch (kind)
            {
                case Kind.FpBase:
                    // Little crafted so far, plenty of potential left.
                    item.forgingPotential = (byte)rng.Next(30, 64);
                    break;
                case Kind.CleanBase:
                    // A fresh base that nobody has touched, with room to craft.
                    item.forgingPotential = (byte)rng.Next(12, 64);
                    break;
                case Kind.Crafted:
                {
                    // Crafted T6/T7 gear, until the forging potential is nearly gone. Some were then duplicated
                    // with a Rune of Creation, some corrupted into a corrupted affix.
                    item.forgingPotential = (byte)rng.Next(0, 9);
                    double variant = rng.NextDouble();
                    if (variant < 0.15)
                    {
                        item.forgingPotential = 0;
                        rolled.Twin = true;
                        rolled.Note = "Rune of Creation";
                        return rolled;
                    }
                    if (variant < 0.40)
                    {
                        Corrupt(item, rng, rolled, forceCorruptedAffix: true);
                        return rolled;
                    }
                    break;
                }
                case Kind.FinalProduct:
                    // The crafter has finished it.
                    item.forgingPotential = (byte)rng.Next(0, 3);
                    break;
                case Kind.Creation:
                    // Rune of Creation duplicates an item and sets the forging potential of both to 0.
                    item.forgingPotential = 0;
                    rolled.Twin = true;
                    rolled.Note = "Rune of Creation";
                    return rolled; // a duplicated item is never also corrupted afterwards
                case Kind.Primordial:
                    Primordialise(item, rng);
                    rolled.Note = "Primordial";
                    break;
            }

            if (kind == Kind.SixAffix)
            {
                // Some are corrupted. Then the rarity byte is set so the game can load the item: five and six
                // affixes carry rarity 4 and keep every affix.
                if (BazaarCustomItems.LastSixCorrupted)
                {
                    Corrupt(item, rng, rolled);
                }
                int ordinary = item.affixes.ToArray().Count(a => a != null && !a.IsSealed);
                item.rarity = AffixLimits.LoadableRarity(ordinary);
                item.sockets = (byte)item.affixes.Count;
                item.RefreshIDAndValues();
                // A listing must survive being packed and read back, or it would lose affixes or potential on the
                // way to the shelf. Say exactly what changed if it does not.
                var restored = new ItemDataUnpacked(item.GetID());
                string changed = restored.affixes.Count != item.affixes.Count
                    ? "affixes " + item.affixes.Count + "->" + restored.affixes.Count
                    : restored.forgingPotential != item.forgingPotential
                        ? "forging potential " + item.forgingPotential + "->" + restored.forgingPotential
                        : restored.legendaryPotential != item.legendaryPotential || restored.weaversWill != item.weaversWill
                            ? "legendary/weaver potential"
                            : "";
                if (changed.Length > 0)
                {
                    Fail(kind, "six-affix item changed when packed (" + changed + ", rarity " + item.rarity + ", " + ordinary + " ordinary, "
                        + (item.affixes.ToArray().Any(x => x != null && x.IsSealed) ? "sealed" : "no seal") + ")");
                    return null;
                }
                rolled.Note = "Six affixes" + (rolled.Note.Length > 0 ? "; " + rolled.Note : "");
            }

            bool unique = item.rarity >= 7;
            if (rng.NextDouble() < (unique ? CorruptedUniqueChance : CorruptedChance) && kind != Kind.CleanBase && !IsCustom(kind))
            {
                Corrupt(item, rng, rolled);
            }
            return rolled;
        }
        catch (Exception ex)
        {
            Fail(kind, Describe(ex));
            return null;
        }
    }

    private static string Describe(Exception ex)
    {
        string trace = ex.StackTrace ?? "";
        return ex.Message + " @ " + (trace.Length > 260 ? trace.Substring(0, 260) : trace).Replace('\r', ' ').Replace('\n', ' ');
    }

    /// <summary>
    /// Corrupts the item the way the game does: usually with an outcome rather than only the flag. Some
    /// gain a corrupted affix (a strong one), some a weak one, some lose affix tiers, some only change
    /// base, and some are simply corrupted with nothing else.
    /// </summary>
    private static void Corrupt(ItemDataUnpacked item, Random rng, Rolled rolled, bool forceCorruptedAffix = false)
    {
        double r = forceCorruptedAffix ? 0.45 : rng.NextDouble();
        CorruptionOutcome outcome;
        bool positive = true;
        if (r < 0.30)
        {
            outcome = CorruptionOutcome.None;
        }
        else if (r < 0.60)
        {
            outcome = CorruptionOutcome.AddsCorruptedAffix;
        }
        else if (r < 0.75)
        {
            outcome = CorruptionOutcome.AddsLowTierCorruptedAffix;
        }
        else if (r < 0.90)
        {
            outcome = CorruptionOutcome.RemovesAffixTiers;
            positive = false;
        }
        else
        {
            outcome = CorruptionOutcome.RerollSubtype;
        }

        if (outcome != CorruptionOutcome.None)
        {
            try
            {



                Actor player = Scripts.Refs_Manager.player_actor;
                if (!player.IsNullOrDestroyed())
                {
                    item.ApplyCorruptionOutcome(player, positive, outcome, false, 100, out int added, out int toRemove, out bool sealedAffix);
                    rolled.Note = outcome.ToString();
                }
            }
            catch (Exception ex)
            {
                // Fall back to a plain corruption rather than dropping the listing.
                Fail(Kind.Common, "corruption outcome " + outcome + ": " + Describe(ex));
            }
        }
        item.CorruptWithoutEffect();
        item.SetAsCorrupted();
        item.forgingPotential = 0;
        rolled.Corrupted = true;
    }

    // The generator's rarity argument is the item rarity byte: for gear that is the number of affixes it should
    // hold. Exalted gear is a full item, so ask for 4 to 6.
    private static int ExaltedRarity(Random rng) => rng.Next(4, 7);

    /// <summary>
    /// Evolves a T7 affix into a sealed primordial T8 one, the way the game does it (and the way the force-drop
    /// tool does): seed an affix at T7, then let the game seal it as primordial.
    /// </summary>
    private static void Primordialise(ItemDataUnpacked item, Random rng)
    {
        if (item.affixes == null || item.affixes.Count == 0 || item.hasSealedPrimordialAffix)
        {
            return;
        }
        ItemAffix seed = null;
        foreach (ItemAffix a in item.affixes)
        {
            if (a != null && !a.IsSealed && a.affixTier >= 6)
            {
                seed = a;
                break;
            }
        }
        if (seed == null)
        {
            // No T7 to evolve: raise a random unsealed affix to the seed tier first.
            var open = new List<ItemAffix>();
            foreach (ItemAffix a in item.affixes)
            {
                if (a != null && !a.IsSealed)
                {
                    open.Add(a);
                }
            }
            if (open.Count == 0)
            {
                return;
            }
            seed = open[rng.Next(open.Count)];
            seed.affixTier = 6;
        }
        item.MakeAffixSealedPrimordialAffix(seed, true);
        item.RefreshIDAndValues();
    }

    private static ItemDataUnpacked Make(GenerateItems gen, Kind kind, int stall, int[] stallTypes, int ilvl, Random rng)
    {
        switch (kind)
        {
            case Kind.Crafted:
                // Crafted gear is T6/T7: take the better of a few exalted rolls, then spend the potential.
                return BestOf(3, () => Initialise(gen, stall, stallTypes, MonolithItemLevel, rng, ExaltedRarity(rng), true));
            case Kind.Primordial:
                return BestOf(FinalProductAttempts, () => Initialise(gen, stall, stallTypes, MonolithItemLevel, rng, ExaltedRarity(rng), true));
            case Kind.Creation:
                // Players spend a Rune of Creation on something close to perfect.
                return BestOf(FinalProductAttempts, () => Initialise(gen, stall, stallTypes, MonolithItemLevel, rng, ExaltedRarity(rng), true));
            case Kind.Monolith:
                return Initialise(gen, stall, stallTypes, MonolithItemLevel, rng, rarity: ExaltedRarity(rng), forceExalted: true);
            case Kind.LpUnique:
                return RollUnique(gen, stall, stallTypes, ilvl, rng, legendary: false, bossOnly: false, minLp: PickLp(rng));
            case Kind.WeaversWill:
                return RollUnique(gen, stall, stallTypes, ilvl, rng, legendary: false, bossOnly: false, minLp: 0, weaver: true);
            case Kind.Legendary:
            {
                // The game never builds these by itself: take a unique and spend its potential on extra affixes.
                ItemDataUnpacked unique = RollUnique(gen, stall, stallTypes, ilvl, rng, legendary: false, bossOnly: false, minLp: 0);
                if (unique == null || unique.rarity != 7)
                {
                    return null; // sets and the like cannot become legendary
                }
                double r = rng.NextDouble();
                int spent = r < 0.40 ? 1 : r < 0.70 ? 2 : r < 0.90 ? 3 : 4;
                if (BazaarCustomItems.AddLegendaryAffixes(unique, spent, rng) == 0)
                {
                    return null;
                }
                unique.legendaryPotential = 0;
                return unique;
            }
            case Kind.BossUnique:
                return RollUnique(gen, stall, stallTypes, ilvl, rng, legendary: false, bossOnly: true, minLp: 0);
            case Kind.Jackpot:
                // Lucky: the best of many exalted drops, or a top legendary-potential unique.
                return rng.NextDouble() < 0.5
                    ? BestOf(JackpotAttempts, () => Initialise(gen, stall, stallTypes, MonolithItemLevel, rng, ExaltedRarity(rng), true))
                    : BestOf(6, () => RollUnique(gen, stall, stallTypes, ilvl, rng, false, false, 4));
            case Kind.FinalProduct:
                return BestOf(FinalProductAttempts, () => Initialise(gen, stall, stallTypes, MonolithItemLevel, rng, ExaltedRarity(rng), true));
            case Kind.FpBase:
                // One to four affixes: magic or rare, never plain, never exalted.
                return Initialise(gen, stall, stallTypes, ilvl, rng, rarity: rng.Next(1, 5), forceExalted: false);
            case Kind.CleanBase:
                return Initialise(gen, stall, stallTypes, ilvl, rng, rarity: 0, forceExalted: false);
            default:
                return Common(gen, stall, stallTypes, ilvl, rng);
        }
    }

    // Legendary potential on LP-farmed uniques: mostly low, rarely the top.
    private static int PickLp(Random rng)
    {
        double r = rng.NextDouble();
        return r < 0.50 ? 1 : r < 0.80 ? 2 : r < 0.95 ? 3 : 4;
    }

    /// <summary>The most valuable of several attempts: luck, or a crafter's selection, made concrete.</summary>
    /// <summary>1 normally; less while the shelves are first being stocked, so each listing costs less to make.</summary>
    public static double AttemptScale = 1.0;

    private static ItemDataUnpacked BestOf(int attempts, Func<ItemDataUnpacked> roll)
    {
        attempts = Math.Max(1, (int)Math.Round(attempts * AttemptScale));
        ItemDataUnpacked best = null;
        long bestValue = -1;
        for (int i = 0; i < attempts; i++)
        {
            ItemDataUnpacked candidate = roll();
            if (candidate == null || !Balanced(candidate, _balanceRng))
            {
                continue;
            }
            long value = BazaarPricing.Appraise(candidate.TryCast<ItemData>(), candidate.LvlReq, false).Fair;
            if (value > bestValue)
            {
                bestValue = value;
                best = candidate;
            }
        }
        return best;
    }

    private static void Fail(Kind kind, string why)
    {
        _failures.TryGetValue(kind, out int n);
        _failures[kind] = n + 1;
        if (n < 3)
        {
            OfflineBazaarMarket.Crumb("factory: " + kind + " failed (" + why + ")");
        }
    }

    private static int BaseType(int stall, int[] stallTypes, Random rng) =>
        stallTypes != null && stallTypes.Length > 0 ? stallTypes[rng.Next(stallTypes.Length)] : stall;

    private static ItemDataUnpacked Common(GenerateItems gen, int stall, int[] stallTypes, int ilvl, Random rng)
    {
        if (stallTypes != null && stallTypes.Any(t => t > 22))
        {
            // Idol and altar stalls: the shop generator never rolls these. Class idols need the advanced
            // generator (it can aim at a class); everything else, and any failure, uses the nemesis one,
            // which asks for the item type directly.
            if (stall >= 24 && stall <= 28)
            {
                try
                {
                    ItemDataUnpacked idol = Initialise(gen, stall, stallTypes, ilvl, rng, rarity: -1, forceExalted: false);
                    if (idol != null)
                    {
                        return idol;
                    }
                }
                catch (Exception ex)
                {
                    Fail(Kind.Common, "class idol: " + Describe(ex));
                }
            }
            return gen.GenerateItemForNemesis(false, ilvl, false, false, new Il2CppSystem.Nullable<int>(), BaseType(stall, stallTypes, rng));
        }
        return gen.GenerateItemForShop(false, ilvl);
    }

    /// <summary>The game's own random item generator, with the parts of the item we care about pinned.</summary>
    private static ItemDataUnpacked Initialise(
        GenerateItems gen,
        int stall,
        int[] stallTypes,
        int ilvl,
        Random rng,
        int rarity,
        bool forceExalted,
        int subType = -1,
        int uniqueId = 0,
        int minLegendaryPotential = 0
    )
    {
        int baseType = BaseType(stall, stallTypes, rng);
        // Every optional argument of this call is an object, and its default is null: the interop layer throws on
        // a null object, so each one must be passed explicitly, as an empty "no value" or an empty collection.
        // Class idol stalls (24..28) ask for that class directly, since the shop and nemesis generators only ever
        // hand out your own class.
        bool classIdol = stall >= 24 && stall <= 28;
        return gen.initialiseRandomItemData(
            false,
            ilvl,
            false,
            ItemLocationTag.None,
            baseType,
            subType,
            rarity,
            -1,
            uniqueId,
            forceExalted,
            minLegendaryPotential,
            factionID: new Il2CppSystem.Nullable<Il2CppLE.Factions.FactionID>(),
            replacementClassID: classIdol ? new Il2CppSystem.Nullable<int>(stall - 24) : new Il2CppSystem.Nullable<int>(),
            corruption: new Il2CppSystem.Nullable<int>(),
            requiredSubTypeAffixEffectiveness: new Il2CppSystem.Nullable<ItemList.AffixEffectiveness>(),
            rollModifiers: new GenerateItems.ItemRollModifiers(),
            forbiddenUniqueDroppableAffixes: new Il2CppSystem.Collections.Generic.List<ushort>(),
            specialAffix: new Il2CppSystem.Nullable<AffixList.SpecialAffixType>(),
            minSpecialAffixTier: new Il2CppSystem.Nullable<int>(),
            overrideLegendaryPotential: new Il2CppSystem.Nullable<int>()
        );
    }

    private static ItemDataUnpacked RollUnique(
        GenerateItems gen,
        int stall,
        int[] stallTypes,
        int ilvl,
        Random rng,
        bool legendary,
        bool bossOnly,
        int minLp,
        bool weaver = false
    )
    {
        if (stall >= 23)
        {
            return null; // idols and altars have no uniques
        }
        // Boss uniques are the ones that do not drop at random, so ask for any and keep only those.
        for (int tries = 0; tries < (bossOnly ? 12 : 1); tries++)
        {
            bool found = UniqueList.rollUnique(
                out ushort id,
                (byte)Math.Min(ilvl, 100),
                UniqueList.SetRequirement.NoRequirement,
                true,
                (byte)BaseType(stall, stallTypes, rng),
                false,
                0,
                !bossOnly,
                false,
                0,
                false,
                0,
                false,
                false,
                new Il2CppSystem.Collections.Generic.List<byte>(),
                !bossOnly, // farmed uniques are of one kind or the other: legendary potential or weaver's will
                weaver ? UniqueList.LegendaryType.WeaversWill : UniqueList.LegendaryType.LegendaryPotential,
                new GenerateItems.ItemRollModifiers()
            );
            if (!found || id == 0)
            {
                continue;
            }
            UniqueList.Entry entry = UniqueList.getUnique(id);
            if (bossOnly && (entry == null || entry.canDropRandomly))
            {
                continue;
            }
            int subType = 0;
            if (entry != null && entry.subTypes != null && entry.subTypes.Count > 0)
            {
                subType = entry.subTypes[rng.Next(entry.subTypes.Count)];
            }
            int uniqueRarity = legendary ? 9 : entry != null && entry.isSetItem ? 8 : 7;
            return Initialise(gen, stall, stallTypes, ilvl, rng, uniqueRarity, false, subType, id, minLp);
        }
        return null;
    }
}
