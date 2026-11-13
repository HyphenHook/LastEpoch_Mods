using LastEpoch_Hud.Scripts.Core.ForceDrop;
using LastEpoch_Hud.Scripts.Core.Items;

namespace LastEpoch_Hud.Tests.Core.ForceDrop;

public class ForceDropLegalRulesTests
{
    [Theory]
    [InlineData(0, 5)]
    [InlineData(34, 5)]
    [InlineData(35, 6)]
    [InlineData(54, 6)]
    [InlineData(55, 7)]
    public void RuneLevelThresholdsDoNotUsePreCorruptedAreaDropThresholds(int level, int maximum)
    {
        Assert.Equal(maximum, ForceDropLegalTiers.RuneMaximumDisplayTier(true, level));
        Assert.Equal(7, ForceDropLegalTiers.RuneMaximumDisplayTier(false, level));
    }

    [Fact]
    public void WeaverAndHereticalAffixesUseTheirOwnIdolRoutes()
    {
        Assert.False(
            ForceDropLegalRules.OrdinaryFamilyAllowed(
                ForceDropAffixFamily.IdolWeaver,
                false,
                false,
                true
            )
        );
        Assert.True(
            ForceDropLegalRules.OrdinaryFamilyAllowed(
                ForceDropAffixFamily.IdolWeaver,
                false,
                false,
                true,
                true
            )
        );
        Assert.False(
            ForceDropLegalRules.OrdinaryFamilyAllowed(
                ForceDropAffixFamily.IdolEnchantment,
                false,
                false,
                true,
                true
            )
        );
        Assert.True(
            ForceDropLegalRules.OrdinaryFamilyAllowed(
                ForceDropAffixFamily.IdolEnchantment,
                false,
                false,
                true,
                false,
                true
            )
        );
        Assert.True(ForceDropLegalRules.SlotAllowed(1, true, false, false, true));
        Assert.True(ForceDropLegalRules.SlotAllowed(3, true, false, false, true));
        Assert.True(ForceDropLegalRules.SlotAllowed(4, true, false, false, true));
        Assert.False(ForceDropLegalRules.SlotAllowed(AffixLimits.SealedSlot, true, false, false));
        Assert.False(
            ForceDropLegalRules.SlotAllowed(AffixLimits.ForceDropRows, true, false, false)
        );
    }

    [Fact]
    public void EveryOrdinarySlotTakesAnyAffixAndUniquesCannotUseSeals()
    {
        for (int slot = 0; slot < AffixLimits.OrdinarySlots; slot++)
        {
            Assert.True(ForceDropLegalRules.SlotAllowed(slot, false, false, false));
            Assert.True(ForceDropLegalRules.SlotAllowed(slot, false, true, false));
        }
        Assert.True(ForceDropLegalRules.SlotAllowed(AffixLimits.SealedSlot, false, false, false));
        Assert.True(
            ForceDropLegalRules.SlotAllowed(AffixLimits.PrimordialSlot, false, false, false)
        );
        // Seals work on every rarity: uniques and sets included.
        Assert.True(ForceDropLegalRules.SlotAllowed(AffixLimits.SealedSlot, false, true, false));
        Assert.True(
            ForceDropLegalRules.SlotAllowed(AffixLimits.PrimordialSlot, false, true, false)
        );
        Assert.True(ForceDropLegalRules.SlotAllowed(AffixLimits.SealedSlot, false, false, true));
        Assert.False(ForceDropLegalRules.SlotAllowed(0, false, false, true));
    }

    [Fact]
    public void LowTierCorruptionDoesNotExposeT7EvenWithEightDefinitionTiers()
    {
        int mask = ForceDropLegalTiers.FromWeights(new float[] { 1, 1, 1, 0, 0, 0, 0 }, 8, 127);
        Assert.Equal(3, ForceDropLegalTiers.MaximumDisplayTier(mask));
        Assert.Equal(3, ForceDropLegalTiers.ClampDisplayTier(mask, 7));
        Assert.False(ForceDropLegalTiers.Supports(mask, 6));
    }

    [Fact]
    public void OneTierIdolAffixCannotBorrowEquipmentTiers()
    {
        int mask = ForceDropLegalTiers.FromWeights(new float[] { 1, 1, 1, 1, 1, 1, 1 }, 1, 127);
        Assert.Equal(1, mask);
        Assert.Equal(1, ForceDropLegalTiers.ClampDisplayTier(mask, 7));
    }

    [Fact]
    public void NativeLevelRestrictionsAndWeightHolesAreBothPreserved()
    {
        int mask = ForceDropLegalTiers.FromWeights(new float[] { 1, 0, 1, 1, 1, 1, 1 }, 8, 15);
        Assert.Equal(13, mask);
        Assert.False(ForceDropLegalTiers.Supports(mask, 1));
        Assert.False(ForceDropLegalTiers.Supports(mask, 4));
        Assert.Equal(1, ForceDropLegalTiers.ClampDisplayTier(mask, 2));
        Assert.Equal(4, ForceDropLegalTiers.ClampDisplayTier(mask, 7));
    }

    [Fact]
    public void NoPositiveTierHasNoUsableSelection()
    {
        int mask = ForceDropLegalTiers.FromWeights(new float[] { 0, -1, float.NaN }, 8, 127);
        Assert.Equal(0, mask);
        Assert.Equal(0, ForceDropLegalTiers.ClampDisplayTier(mask, 7));
        Assert.False(ForceDropLegalTiers.Supports(mask, -1));
        Assert.False(ForceDropLegalTiers.Supports(255, 7));
    }

    [Fact]
    public void SetShardIsLegalOnBaseEquipmentButCannotBecomeLegendaryDonorAffix()
    {
        Assert.True(
            ForceDropLegalRules.OrdinaryFamilyAllowed(ForceDropAffixFamily.Set, false, false, false)
        );
        Assert.False(
            ForceDropLegalRules.OrdinaryFamilyAllowed(ForceDropAffixFamily.Set, true, false, false)
        );
    }

    [Fact]
    public void ChampionMembershipIsRequiredForPersonalLegendaryTransfer()
    {
        Assert.False(
            ForceDropLegalRules.OrdinaryFamilyAllowed(
                ForceDropAffixFamily.Personal,
                true,
                false,
                false
            )
        );
        Assert.True(
            ForceDropLegalRules.OrdinaryFamilyAllowed(
                ForceDropAffixFamily.Personal,
                true,
                true,
                false
            )
        );
        Assert.True(
            ForceDropLegalRules.OrdinaryFamilyAllowed(
                ForceDropAffixFamily.Experimental,
                true,
                false,
                false
            )
        );
    }

    [Theory]
    [InlineData(ForceDropAffixFamily.Corrupted)]
    [InlineData(ForceDropAffixFamily.UniqueModifier)]
    [InlineData(ForceDropAffixFamily.IdolEnchantment)]
    [InlineData(ForceDropAffixFamily.IdolWeaver)]
    public void SeparateModifierRoutesNeverEnterOrdinaryEquipmentSlots(ForceDropAffixFamily family)
    {
        Assert.False(ForceDropLegalRules.OrdinaryFamilyAllowed(family, false, false, false));
        Assert.False(ForceDropLegalRules.OrdinaryFamilyAllowed(family, true, false, false));
    }

    [Fact]
    public void OrdinaryIdolUsesEveryOrdinarySlotButNoSeals()
    {
        for (int slot = 0; slot < AffixLimits.OrdinarySlots; slot++)
            Assert.True(ForceDropLegalRules.SlotAllowed(slot, true, false, false));
        Assert.False(ForceDropLegalRules.SlotAllowed(AffixLimits.SealedSlot, true, false, false));
        Assert.False(
            ForceDropLegalRules.SlotAllowed(AffixLimits.PrimordialSlot, true, false, false)
        );
        Assert.False(ForceDropLegalRules.SlotAllowed(0, true, true, false));
    }

    [Fact]
    public void EquipmentHasIndependentRegularAndPrimordialSealSlots()
    {
        Assert.True(ForceDropLegalRules.SlotAllowed(AffixLimits.SealedSlot, false, false, false));
        Assert.True(
            ForceDropLegalRules.SlotAllowed(AffixLimits.PrimordialSlot, false, false, false)
        );
        Assert.True(
            ForceDropLegalRules.SlotAllowed(AffixLimits.PrimordialSlot, false, true, false)
        );
        Assert.False(
            ForceDropLegalRules.SlotAllowed(AffixLimits.PrimordialSlot, true, false, false)
        );
        Assert.False(
            ForceDropLegalRules.SlotAllowed(AffixLimits.ForceDropRows, false, false, false)
        );
    }

    [Fact]
    public void SetItemsTakeSealsButNoOrdinaryAffixes()
    {
        Assert.True(ForceDropLegalRules.SlotAllowed(AffixLimits.SealedSlot, false, true, true));
        Assert.False(ForceDropLegalRules.SlotAllowed(0, false, true, true));
    }
}
