using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Affixes;

/// <summary>Implicits, mods and tooltip entries of each custom unique.</summary>
public static class CustomUniqueAffixes
{
    /// <summary>Tooltip entry that shows the item description line.</summary>
    public const byte DescriptionEntry = 128;

    public static readonly IReadOnlyList<CustomBaseImplicit> HeadhunterImplicits =
        new CustomBaseImplicit[]
        {
            new(CustomItemStat.Health, CustomItemTag.None, CustomItemModType.Added, 0, 25f, 40f),
        };

    public static readonly IReadOnlyList<CustomBaseImplicit> SandsOfSilkImplicits =
        new CustomBaseImplicit[]
        {
            new(
                CustomItemStat.DodgeRating,
                CustomItemTag.None,
                CustomItemModType.Added,
                0,
                153f,
                204f
            ),
        };

    public static readonly IReadOnlyList<CustomBaseImplicit> EssentiaSanguisImplicits =
        new CustomBaseImplicit[]
        {
            new(
                CustomItemStat.DodgeRating,
                CustomItemTag.None,
                CustomItemModType.Added,
                0,
                50f,
                50f
            ),
        };

    public static readonly IReadOnlyList<CustomBaseImplicit> MagebloodImplicits =
        new CustomBaseImplicit[]
        {
            new(CustomItemStat.Strength, CustomItemTag.None, CustomItemModType.Added, 0, 25f, 35f),
            new(CustomItemStat.Dexterity, CustomItemTag.None, CustomItemModType.Added, 0, 30f, 50f),
        };

    public static readonly IReadOnlyList<CustomUniqueMod> MagebloodMods =
        System.Array.Empty<CustomUniqueMod>();

    public static readonly IReadOnlyList<byte> MagebloodTooltip = new byte[] { DescriptionEntry };

    public static readonly IReadOnlyList<CustomUniqueMod> HeadhunterMods = new CustomUniqueMod[]
    {
        new(CustomItemStat.Strength, CustomItemTag.None, CustomItemModType.Added, 40f, 55f, true),
        new(CustomItemStat.Dexterity, CustomItemTag.None, CustomItemModType.Added, 40f, 55f, true),
        new(CustomItemStat.Health, CustomItemTag.None, CustomItemModType.Added, 50f, 60f, true),
        new(
            CustomItemStat.Damage,
            CustomItemTag.None,
            CustomItemModType.Increased,
            0.2f,
            0.3f,
            true
        ),
    };

    public static readonly IReadOnlyList<CustomUniqueMod> SandsOfSilkMods = new CustomUniqueMod[]
    {
        new(
            CustomItemStat.DodgeRating,
            CustomItemTag.None,
            CustomItemModType.Increased,
            0.5f,
            1f,
            true
        ),
        new(CustomItemStat.Mana, CustomItemTag.None, CustomItemModType.Added, 50f, 80f, true),
        new(CustomItemStat.Dexterity, CustomItemTag.None, CustomItemModType.Added, 10f, 20f, true),
        new(
            CustomItemStat.Intelligence,
            CustomItemTag.None,
            CustomItemModType.Added,
            10f,
            20f,
            true
        ),
        new(
            CustomItemStat.FireResistance,
            CustomItemTag.None,
            CustomItemModType.Increased,
            0.1f,
            0.15f,
            true
        ),
        new(
            CustomItemStat.IncreasedCooldownRecoverySpeed,
            CustomItemTag.None,
            CustomItemModType.Increased,
            0.15f,
            0.3f,
            true
        ),
    };

    public static readonly IReadOnlyList<CustomUniqueMod> MjolnerMods = new CustomUniqueMod[]
    {
        new(
            CustomItemStat.Damage,
            CustomItemTag.Lightning,
            CustomItemModType.Increased,
            0.8f,
            1f,
            true
        ),
        new(
            CustomItemStat.Damage,
            CustomItemTag.Physical,
            CustomItemModType.Increased,
            0.8f,
            1.2f,
            true
        ),
    };

    public static readonly IReadOnlyList<CustomUniqueMod> EssentiaSanguisMods =
        new CustomUniqueMod[]
        {
            new(
                CustomItemStat.DodgeRating,
                CustomItemTag.None,
                CustomItemModType.Increased,
                0.5f,
                0.7f,
                true
            ),
            new(
                CustomItemStat.HealthLeech,
                CustomItemTag.None,
                CustomItemModType.Added,
                0.5f,
                1f,
                true
            ),
            new(
                CustomItemStat.Damage,
                CustomItemTag.Lightning,
                CustomItemModType.Added,
                30f,
                50f,
                true
            ),
            new(
                CustomItemStat.Intelligence,
                CustomItemTag.None,
                CustomItemModType.Added,
                15f,
                25f,
                true
            ),
            new(
                CustomItemStat.LightningResistance,
                CustomItemTag.None,
                CustomItemModType.Increased,
                0.25f,
                0.35f,
                true
            ),
        };

    public static readonly IReadOnlyList<byte> HeadhunterTooltip = new byte[]
    {
        0,
        1,
        2,
        3,
        DescriptionEntry,
    };

    public static readonly IReadOnlyList<byte> SandsOfSilkTooltip = new byte[] { 0, 1, 2, 3, 4, 5 };

    public static readonly IReadOnlyList<byte> EssentiaSanguisTooltip = new byte[]
    {
        0,
        1,
        2,
        3,
        4,
        DescriptionEntry,
    };

    public static readonly IReadOnlyList<byte> MjolnerTooltip = new byte[]
    {
        0,
        1,
        DescriptionEntry,
    };
}
