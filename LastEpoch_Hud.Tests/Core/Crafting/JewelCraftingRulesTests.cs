using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.Core.ForceDrop;
using LastEpoch_Hud.Scripts.Core.Items;

namespace LastEpoch_Hud.Tests.Core.Crafting;

public class JewelCraftingRulesTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void JewelRecombinationAcceptsSixOfEitherSideButOrdinaryEquipmentDoesNot(int side)
    {
        var pool = Enumerable.Range(1, 6).Select(id => new RecombAffix
        {
            AffixId = id, Placement = side, DisplayedTier = 7, Weight = 1,
        }).ToArray();
        int jewelSide = EquipmentTypeRules.TransferSideLimit(44);
        Assert.Equal("", RecombinatorMath.Validate(pool, 6, jewelSide));
        Assert.NotEqual("", RecombinatorMath.Validate(pool));
        Assert.True(RecombinatorMath.SuccessChance(pool, maxPerSide: jewelSide) > 0);
        Assert.Equal(0, RecombinatorMath.SuccessChance(pool));
        Assert.Equal(6, RecombinatorMath.Draw(pool, 6, () => 0, maxPerSide: jewelSide).Count);
        Assert.Equal(3, RecombinatorMath.Draw(pool, 6, () => 0).Count);
        // An encapsulated ordinary affix uses one of the six slots, including its side.
        Assert.Equal(5, RecombinatorMath.Draw(pool, 6, () => 0, prefixes: side == 1 ? 1 : 0,
            suffixes: side == 2 ? 1 : 0, maxPerSide: jewelSide).Count);
        Assert.NotEqual("", RecombinatorMath.Validate(pool, 5, jewelSide));
    }

    [Fact]
    public void JewelReflectionUsesTheExistingFourthToSixthAffixProgression()
    {
        Assert.NotEqual("", ReflectorMath.WhyNot(3, 4));
        Assert.Equal("", ReflectorMath.WhyNot(4, 1));
        Assert.Equal("", ReflectorMath.WhyNot(5, 1));
        Assert.NotEqual("", ReflectorMath.WhyNot(6, 1));
        Assert.Equal(4, EquipmentTypeRules.JewelCraftedAffixes);
        Assert.Equal(6, EquipmentTypeRules.JewelTransferAffixes);
    }

    [Theory]
    [InlineData(43, true)]
    [InlineData(44, true)]
    [InlineData(21, false)]
    [InlineData(25, false)]
    public void NativeCorruptionOnlyPolicyIsScopedToCharmsAndJewels(int type, bool expected) =>
        Assert.Equal(expected, EquipmentTypeRules.IsCorruptionOnly(type));

    [Fact]
    public void ForceDropCountsProtectedJewelAffixesTowardTheSixTotalLimit()
    {
        var ordinary = Enumerable.Range(1, 6).Select(id =>
            new ResolvedForceDropAffix(id, 6, 255, ForceDropSeal.None)).ToList();
        var protectedAffixes = new[] {
            new ResolvedForceDropAffix(7, 4, 255, ForceDropSeal.Regular),
            new ResolvedForceDropAffix(8, 7, 255, ForceDropSeal.Primordial),
        };
        ResolvedForceDrop Build(int type, IEnumerable<ResolvedForceDropAffix> affixes) =>
            new(type, 0, 0, 4, 40, 2, 0, false, Array.Empty<int>(), Array.Empty<int>(), affixes, Array.Empty<int>(), null);
        var request = Build(44, ordinary.Take(4).Concat(protectedAffixes));
        Assert.Equal(6, request.Affixes.Count);
        Assert.Equal(2, request.LegendaryPotential);
        Assert.Throws<ArgumentException>(() => Build(44, ordinary.Concat(protectedAffixes)));
        Assert.Equal(8, Build(21, ordinary.Concat(protectedAffixes)).Affixes.Count);
        ordinary.Add(new ResolvedForceDropAffix(9, 6, 255, ForceDropSeal.None));
        Assert.Throws<ArgumentException>(() => Build(44, ordinary));
        Assert.Equal(7, Build(21, ordinary).Affixes.Count);
        Assert.True(ForceDropLegalRules.SlotAllowed(5, false, false, false, itemType: 44));
        Assert.False(ForceDropLegalRules.SlotAllowed(6, false, false, false, itemType: 44));
        Assert.True(ForceDropLegalRules.SlotAllowed(AffixLimits.SealedSlot, false, false, false, itemType: 44));
    }

    [Fact]
    public void RecombinationWithNoLegalAffixesDoesNotInventASlot() =>
        Assert.Equal(0, RecombinatorMath.ResultCount(0, 0, 0, () => 0));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void CraftableTierRerollsCannotSelectExaltedOnlyJewelDefinitions(int memoryTier)
    {
        // These are minimum tiers from the Jewel catalog: regular, T6-only, T7-only.
        int[] minimums = { 1, 6, 7 };
        Assert.Equal(new[] { 1 }, minimums.Where(ChaosRerollMath.CanRollDefinition));
        Assert.Equal(new[] { 1 }, minimums.Where(min => ObscuredMemory.SupportsTier(memoryTier - 1, min, 7)));
    }

    [Fact]
    public void ExaltedMemoriesOnlyOfferDefinitionsAvailableAtTheirRetainedTier()
    {
        Assert.True(ObscuredMemory.SupportsTier(5, 6, 7));
        Assert.False(ObscuredMemory.SupportsTier(5, 7, 7));
        Assert.True(ObscuredMemory.SupportsTier(6, 7, 7));
        Assert.False(ObscuredMemory.SupportsTier(6, 6, 6));
        Assert.False(ObscuredMemory.SupportsTier(7, 1, 7));
    }
}
