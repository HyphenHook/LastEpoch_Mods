using System;
using System.Collections.Generic;
using System.Linq;
using LastEpoch_Hud.Scripts.Core.Items;

namespace LastEpoch_Hud.Scripts.Core.ForceDrop;

public sealed class PackedForceDropAffix
{
    public int Id { get; }
    public int Tier { get; }
    public int Roll { get; }
    public ForceDropSeal Seal { get; }
    public int SpecialType { get; }
    public int AffixType { get; }

    public PackedForceDropAffix(
        int id,
        int tier,
        int roll,
        ForceDropSeal seal,
        int specialType,
        int affixType = 0
    )
    {
        Id = id;
        Tier = tier;
        Roll = roll;
        Seal = seal;
        SpecialType = specialType;
        AffixType = affixType;
    }

    public string Signature =>
        Id + ":" + Tier + ":" + Roll + ":" + Seal + ":" + SpecialType + ":" + AffixType;
}

// Compare data decoded from the final saved ID, including ordinary drops. Affix
// ordering may change during native sealing; seal ownership must never change.
public sealed class ForceDropPackingSnapshot
{
    public int ItemType { get; }
    public int SubType { get; }
    public int UniqueId { get; }
    public int Rarity { get; }
    public int ForgingPotential { get; }
    public int LegendaryPotential { get; }
    public int WeaversWill { get; }
    public bool Corrupted { get; }
    public int Sockets { get; }
    public bool RegularSeal { get; }
    public bool PrimordialSeal { get; }
    public bool CorruptionSeal { get; }
    public IReadOnlyList<int> ImplicitRolls { get; }
    public IReadOnlyList<int> UniqueRolls { get; }
    public IReadOnlyList<PackedForceDropAffix> Affixes { get; }

    public ForceDropPackingSnapshot(
        int itemType,
        int subType,
        int uniqueId,
        int rarity,
        int forgingPotential,
        int legendaryPotential,
        int weaversWill,
        bool corrupted,
        int sockets,
        bool regularSeal,
        bool primordialSeal,
        bool corruptionSeal,
        IEnumerable<int> implicitRolls,
        IEnumerable<int> uniqueRolls,
        IEnumerable<PackedForceDropAffix> affixes
    )
    {
        ItemType = itemType;
        SubType = subType;
        UniqueId = uniqueId;
        Rarity = rarity;
        ForgingPotential = forgingPotential;
        LegendaryPotential = legendaryPotential;
        WeaversWill = weaversWill;
        Corrupted = corrupted;
        Sockets = sockets;
        RegularSeal = regularSeal;
        PrimordialSeal = primordialSeal;
        CorruptionSeal = corruptionSeal;
        ImplicitRolls = Array.AsReadOnly(implicitRolls.ToArray());
        UniqueRolls = Array.AsReadOnly(uniqueRolls.ToArray());
        Affixes = Array.AsReadOnly(affixes.ToArray());
    }

    public string IntegrityError(bool decoded = false, bool allowIllegalT8 = false)
    {
        if (Affixes.Any(a => a == null))
            return "Missing affix data";
        // Decoded native items can report zero sockets while retaining the full
        // affix list, on both unique and base routes, with or without corruption.
        // This is not the serialized affix count. Before packing, still validate
        // the live base count; after decoding, verify the complete affix list.
        if (Sockets < 0 || Sockets > byte.MaxValue)
            return "Socket count is outside the packed range";
        // Native corruption insertion excludes its new sealed affix from the
        // live base-item count. No other nonzero count mismatch is accepted.
        if (
            !UsesUniqueStorage
            && Sockets != Affixes.Count
            && !(UsesCorruptionStorage && Sockets == Affixes.Count - 1)
            && !(decoded && Sockets == 0)
        )
            return "Socket count does not match the affix count (sockets="
                + Sockets
                + ", affixes="
                + Affixes.Count
                + ")";
        if (Affixes.Count > AffixLimits.MaxTotal)
            return "More than " + AffixLimits.MaxTotal + " affixes";
        if (Affixes.Select(a => a.Id).Distinct().Count() != Affixes.Count)
            return "Duplicate affix IDs";
        if (
            Affixes.Any(a =>
                a.Id < 0
                || a.Id > ushort.MaxValue
                || a.Tier < 0
                || a.Tier > 7
                || a.Roll < 0
                || a.Roll > 255
                || !Enum.IsDefined(typeof(ForceDropSeal), a.Seal)
            )
        )
            return "Affix data is outside the packed range";
        if (Affixes.Count(a => a.Seal == ForceDropSeal.Regular) != (RegularSeal ? 1 : 0))
            return "Regular seal flag does not match its affix";
        int primordialCount = Affixes.Count(a => a.Seal == ForceDropSeal.Primordial);
        if (
            allowIllegalT8
                ? PrimordialSeal != (primordialCount > 0)
                    || Affixes.Any(a => a.Seal == ForceDropSeal.Primordial && a.Tier != 7)
                : primordialCount != (PrimordialSeal ? 1 : 0)
        )
            return "Primordial seal flag does not match its affix";
        if (Affixes.Count(a => a.Seal == ForceDropSeal.Corruption) != (CorruptionSeal ? 1 : 0))
            return "Corruption seal flag does not match its affix";
        if (CorruptionSeal && !Corrupted)
            return "Corruption seal requires a corrupted item";
        return "";
    }

    public string Difference(ForceDropPackingSnapshot actual, bool allowIllegalT8 = false)
    {
        string error = Compare(actual, allowIllegalT8);
        if (error.Length == 0)
            return "";
        return error
            + "; expected={"
            + Describe()
            + "}; decoded={"
            + (actual == null ? "missing" : actual.Describe())
            + "}";
    }

    // Rejected items never reach the ground. Keep their exact construction and
    // decode evidence in the error log instead of guessing from a generic label.
    public string Describe()
    {
        return "base="
            + ItemType
            + ", sub="
            + SubType
            + ", unique="
            + UniqueId
            + ", rarity="
            + Rarity
            + ", fp="
            + ForgingPotential
            + ", lp="
            + LegendaryPotential
            + ", ww="
            + WeaversWill
            + ", corrupted="
            + Corrupted
            + ", sockets="
            + Sockets
            + ", count="
            + Affixes.Count
            + ", regular="
            + RegularSeal
            + ", primordial="
            + PrimordialSeal
            + ", corruption="
            + CorruptionSeal
            + ", implicit=["
            + string.Join(",", ImplicitRolls)
            + "], uniqueRolls=["
            + string.Join(",", UniqueRolls)
            + "], affixes=["
            + string.Join(",", Affixes.Select(a => a == null ? "missing" : a.Signature))
            + "]";
    }

    string Compare(ForceDropPackingSnapshot actual, bool allowIllegalT8)
    {
        string error = IntegrityError(allowIllegalT8: allowIllegalT8);
        if (error.Length != 0)
            return "Before packing: " + error;
        if (actual == null)
            return "Missing decoded item";
        error = actual.IntegrityError(decoded: true, allowIllegalT8: allowIllegalT8);
        if (error.Length != 0)
            return "After packing: " + error;
        if (ItemType != actual.ItemType || SubType != actual.SubType || UniqueId != actual.UniqueId)
            return "Item identity changed during packing";
        if (Rarity != actual.Rarity)
            return "Item rarity changed during packing";
        if (Sockets != actual.Sockets && actual.Sockets != 0)
            return "Item socket count changed during packing";
        if (
            ForgingPotential != actual.ForgingPotential
            || LegendaryPotential != actual.LegendaryPotential
            || WeaversWill != actual.WeaversWill
        )
            return "Item potential changed during packing";
        if (
            Corrupted != actual.Corrupted
            || RegularSeal != actual.RegularSeal
            || PrimordialSeal != actual.PrimordialSeal
            || CorruptionSeal != actual.CorruptionSeal
        )
            return "Item corruption or seal flags changed during packing";
        if (!ImplicitRolls.SequenceEqual(actual.ImplicitRolls))
            return "Implicit rolls changed during packing";
        if (!UniqueRolls.SequenceEqual(actual.UniqueRolls))
            return "Unique rolls changed during packing";
        var expected = Affixes.Select(a => a.Signature).OrderBy(s => s, StringComparer.Ordinal);
        var saved = actual.Affixes.Select(a => a.Signature).OrderBy(s => s, StringComparer.Ordinal);
        if (!expected.SequenceEqual(saved))
            return "Affix identity, tier, roll, special type or seal ownership changed during packing";
        return "";
    }

    bool UsesUniqueStorage => Rarity == 7 || Rarity == 8 || Rarity == 9;

    bool UsesCorruptionStorage => Corrupted && CorruptionSeal;
}
