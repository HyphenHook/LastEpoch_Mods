using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

using LastEpoch_Hud.Scripts.Core.Tuning;
namespace LastEpoch_Hud.Scripts.Core.Crafting;

public sealed class AttunedAffix
{
    public int Id;
    public byte Tier;
    public byte Roll;
    public double Multiplier;

    public AttunedAffix Copy() => new() { Id = Id, Tier = Tier, Roll = Roll, Multiplier = Multiplier };
}

/// <summary>The presence of this record marks a base item as attuned and stores its scaling.</summary>
public sealed class AttunementState
{
    public const double DefaultMinMultiplier = 0.70;
    public const double DefaultMaxMultiplier = 1.30;

    /// <summary>The attunement multiplier range, from Data/CraftingNumbers.csv.</summary>
    public static double MinMultiplier => Tunables.Get("Attunement.MinMultiplier", DefaultMinMultiplier);
    public static double MaxMultiplier => Math.Max(MinMultiplier, Tunables.Get("Attunement.MaxMultiplier", DefaultMaxMultiplier));

    /// <summary>What a saved multiplier may be: wide enough for the defaults too, so narrowing the range never discards an item.</summary>
    public static double LowestValidMultiplier => Math.Min(MinMultiplier, DefaultMinMultiplier);
    public static double HighestValidMultiplier => Math.Max(MaxMultiplier, DefaultMaxMultiplier);

    /// <summary>The highest roll Attunement draws: thousandths above the minimum multiplier.</summary>
    public static int MaxRoll => (int)Math.Round((MaxMultiplier - MinMultiplier) * 1000.0);

    public int ItemType;
    public int SubType;
    public int UniqueId;
    [JsonProperty(Required = Required.Always)]
    public double Multiplier = 1;
    public byte[] Implicits = Array.Empty<byte>();
    public byte[] Unique = Array.Empty<byte>();
    public List<AttunedAffix> Affixes = new();

    public static double RollMultiplier(int roll) => MinMultiplier + Math.Clamp(roll, 0, MaxRoll) / 1000.0;

    /// <summary>Native effect modifiers store the bonus above one, including the post-rounding bonus.</summary>
    public static float ScaleEffectBonus(float bonus, double multiplier) =>
        multiplier == 1 ? bonus : (float)((1.0 + bonus) * multiplier - 1.0);

    public double AffixMultiplier(int id, byte tier, byte roll)
    {
        var affix = Affixes.Find(a => a.Id == id && a.Tier == tier && a.Roll == roll);
        return affix?.Multiplier ?? 1;
    }

    public double ImplicitMultiplier(int itemType, int subType, IEnumerable<byte> rolls) =>
        itemType == ItemType && subType == SubType && rolls != null && Implicits.SequenceEqual(rolls) ? Multiplier : 1;

    public double UniqueMultiplier(int uniqueId, IEnumerable<byte> rolls) =>
        uniqueId == UniqueId && rolls != null && Unique.SequenceEqual(rolls) ? Multiplier : 1;

    public AttunementState Copy() => new()
    {
        ItemType = ItemType,
        SubType = SubType,
        UniqueId = UniqueId,
        Multiplier = Multiplier,
        Implicits = (byte[])Implicits.Clone(),
        Unique = (byte[])Unique.Clone(),
        Affixes = Affixes.Select(a => a.Copy()).ToList(),
    };
}
