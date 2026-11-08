using LastEpoch_Hud.Scripts.Core.ForceDrop;

namespace LastEpoch_Hud.Tests.Core.ForceDrop;

public sealed class ForceDropModeRulesTests
{
    [Theory]
    [InlineData(7, ForceDropSeal.None, ForceDropMode.Illegal, ForceDropSeal.Primordial)]
    [InlineData(6, ForceDropSeal.None, ForceDropMode.Illegal, ForceDropSeal.None)]
    [InlineData(7, ForceDropSeal.None, ForceDropMode.Legal, ForceDropSeal.None)]
    [InlineData(7, ForceDropSeal.Corruption, ForceDropMode.Illegal, ForceDropSeal.Corruption)]
    [InlineData(7, ForceDropSeal.Regular, ForceDropMode.Illegal, ForceDropSeal.Regular)]
    public void OnlyIllegalOrdinaryT8UsesTheObservedNativePrimordialRepresentation(
        int tier,
        ForceDropSeal seal,
        ForceDropMode mode,
        ForceDropSeal expected
    ) => Assert.Equal(expected, ForceDropModeRules.PersistedSeal(tier, seal, mode));

    [Fact]
    public void IllegalRequest_CarriesT8InEveryOrdinarySealedAndCorruptionSlot()
    {
        var selected = Enumerable
            .Range(1, 4)
            .Select(id => new ResolvedForceDropAffix(id, 7, 255, ForceDropSeal.None))
            .Append(new ResolvedForceDropAffix(5, 7, 255, ForceDropSeal.Primordial));
        var request = new ResolvedForceDrop(
            21,
            1,
            0,
            0,
            0,
            0,
            0,
            true,
            new int[3],
            Array.Empty<int>(),
            selected,
            Array.Empty<int>(),
            new ResolvedForceDropAffix(6, 7, 255, ForceDropSeal.Corruption),
            ForceDropMode.Illegal
        );
        Assert.Equal(5, request.Affixes.Count);
        Assert.All(request.Affixes, affix => Assert.Equal(7, affix.Tier));
        Assert.Equal(7, request.Corruption.Tier);
        Assert.True(request.IsIllegal);
    }

    [Theory]
    [InlineData(8, ForceDropMode.Legal, 7)]
    [InlineData(8, ForceDropMode.Illegal, 8)]
    [InlineData(7, ForceDropMode.Illegal, 7)]
    [InlineData(1, ForceDropMode.Illegal, 1)]
    [InlineData(0, ForceDropMode.Illegal, 0)]
    public void IllegalModeCannotInventDefinitionTiers(
        int count,
        ForceDropMode mode,
        int maximum
    ) => Assert.Equal(maximum, ForceDropModeRules.MaximumTier(count, mode));

    [Theory]
    [InlineData(21, 0, ForceDropAffixFamily.Standard, 8, ForceDropMode.Legal, true)]
    [InlineData(21, 7, ForceDropAffixFamily.Standard, 8, ForceDropMode.Legal, true)]
    [InlineData(21, 8, ForceDropAffixFamily.Standard, 8, ForceDropMode.Legal, true)]
    [InlineData(25, 0, ForceDropAffixFamily.Standard, 8, ForceDropMode.Legal, false)]
    [InlineData(21, 0, ForceDropAffixFamily.Set, 8, ForceDropMode.Legal, false)]
    [InlineData(21, 0, ForceDropAffixFamily.Standard, 7, ForceDropMode.Legal, false)]
    [InlineData(21, 7, ForceDropAffixFamily.Standard, 8, ForceDropMode.Illegal, true)]
    [InlineData(21, 7, ForceDropAffixFamily.Standard, 1, ForceDropMode.Illegal, false)]
    public void PrimordialSealUsesItsOwnRoute(
        int type,
        int rarity,
        ForceDropAffixFamily family,
        int tiers,
        ForceDropMode mode,
        bool allowed
    ) =>
        Assert.Equal(
            allowed,
            ForceDropModeRules.CanSealPrimordial(type, rarity, family, tiers, mode)
        );

    [Fact]
    public void SavedUnsealedT8DoesNotDependOnTheCreationToggle()
    {
        Assert.Equal(7, ForceDropModeRules.MaximumPersistedTier(8));
        Assert.Equal(0, ForceDropModeRules.MaximumPersistedTier(1));
    }

    [Fact]
    public void RegularPrimordialAndCorruptionSealsAreSeparate()
    {
        var request = Request(
            ForceDropMode.Legal,
            new[]
            {
                new ResolvedForceDropAffix(13, 6, 42, ForceDropSeal.Regular),
                new ResolvedForceDropAffix(8, 7, 255, ForceDropSeal.Primordial),
            }
        );
        Assert.Equal(ForceDropMode.Legal, request.Mode);
        Assert.Equal(ForceDropSeal.Regular, request.Affixes[0].Seal);
        Assert.Equal(ForceDropSeal.Primordial, request.Affixes[1].Seal);
        Assert.Equal(ForceDropSeal.Corruption, request.Corruption.Seal);
        Assert.Equal(7, request.Affixes[1].Tier);
    }

    [Theory]
    [InlineData(ForceDropMode.Legal)]
    [InlineData(ForceDropMode.Illegal)]
    public void PrimordialCannotBeTwoSealsOrAT7Downgrade(ForceDropMode mode)
    {
        Assert.Throws<ArgumentException>(() =>
            Request(
                mode,
                new[] { new ResolvedForceDropAffix(13, 6, 255, ForceDropSeal.Primordial) }
            )
        );
        Assert.Throws<ArgumentException>(() =>
            Request(
                mode,
                new[]
                {
                    new ResolvedForceDropAffix(13, 7, 255, ForceDropSeal.Primordial),
                    new ResolvedForceDropAffix(8, 7, 255, ForceDropSeal.Primordial),
                }
            )
        );
    }

    [Fact]
    public void IllegalRequestStillRejectsDuplicateAndOutOfBoundsData()
    {
        Assert.Throws<ArgumentException>(() =>
            Request(
                ForceDropMode.Illegal,
                new[]
                {
                    new ResolvedForceDropAffix(13, 7, 255, ForceDropSeal.None),
                    new ResolvedForceDropAffix(13, 6, 255, ForceDropSeal.Regular),
                }
            )
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResolvedForceDropAffix(13, 8, 255, ForceDropSeal.None)
        );
        Assert.True(
            Request(
                ForceDropMode.Illegal,
                new[] { new ResolvedForceDropAffix(13, 7, 255, ForceDropSeal.None) }
            ).IsIllegal
        );
    }

    static ResolvedForceDrop Request(
        ForceDropMode mode,
        IEnumerable<ResolvedForceDropAffix> affixes
    ) =>
        new(
            21,
            1,
            0,
            0,
            100,
            0,
            0,
            true,
            new[] { 255, 255, 255 },
            Array.Empty<int>(),
            affixes,
            Array.Empty<int>(),
            new ResolvedForceDropAffix(1020, 6, 255, ForceDropSeal.Corruption),
            mode
        );
}
