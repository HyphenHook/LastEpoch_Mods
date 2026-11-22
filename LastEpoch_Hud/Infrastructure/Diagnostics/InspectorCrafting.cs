using System;
using System.Linq;
using System.Text;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;

namespace LastEpoch_Hud.Scripts.Dev;

/// <summary>Read-only crafting diagnostics. Called on the game thread through InspectorServer.</summary>
internal static class InspectorCrafting
{
    public static string Describe(string requestedAffix)
    {
        AffixList catalog = AffixList.get();
        if (!string.IsNullOrEmpty(requestedAffix))
        {
            if (!ushort.TryParse(requestedAffix, out ushort id)) return "affixId must be an unsigned integer.";
            if (catalog.IsNullOrDestroyed()) return "Affix catalog is not loaded.";
            var definition = catalog.GetAffix(id);
            return definition.IsNullOrDestroyed() ? $"Affix {id} does not resolve." : Definition(definition);
        }

        var result = new StringBuilder();
        result.AppendLine("Affix catalog: " + (catalog.IsNullOrDestroyed() ? "not loaded" : "loaded"));
        ItemData advancedItem = Items_AdvancedForgeWindow.InspectorItem;
        bool advanced = !advancedItem.IsNullOrDestroyed();
        ItemData item = advanced ? advancedItem : Items_Encapsulation.ForgeItem();
        if (item.IsNullOrDestroyed())
        {
            result.AppendLine("No item in the Forge main slot. Place a test item in the Forge to inspect its rolls.");
            return result.ToString();
        }

        result.AppendLine($"{(advanced ? "Advanced Forge" : "Forge")} item {item.individualID}: type={item.itemType}/{item.subType} rarity={item.rarity} corrupted={item.corrupted} FP={item.forgingPotential}");
        var attunement = Items_Attunement.StateOf(item);
        if (attunement != null)
            result.AppendLine($"Attunement: multiplier={attunement.Multiplier:F3}; implicit factor={Items_AttunementStats.ImplicitFactor(item):F3}");
        var memory = Items_Reminiscence.ObscuredOf(item);
        if (memory != null)
        {
            result.AppendLine($"Obscured Memory: {(AffixList.AffixType)memory.AffixType} T{memory.Tier + 1}; unveiled={memory.Revealed}; saved choices={memory.Choices.Count}");
        }

        result.AppendLine("Native affixes:");
        if (item.affixes != null)
        {
            foreach (ItemAffix affix in item.affixes)
            {
                if (affix == null) continue;
                result.AppendLine($"  id={affix.affixId} {affix.affixName} {affix.affixType} T{affix.affixTier + 1} roll={affix.affixRoll} attached={CraftProtection.IsAttached(item, affix)}");
            }
        }
        return result.ToString();
    }

    /// <summary>Compare native tooltip and stat calculations using independent copies, without spending an orb.</summary>
    public static string ProbeAttunement()
    {
        ItemData source = Items_AdvancedForgeWindow.InspectorItem ?? Items_Encapsulation.ForgeItem();
        if (source.IsNullOrDestroyed()) return "Place an Attuned item in either Forge first.";
        var state = Items_Attunement.StateOf(source);
        if (state == null) return "The Forge item has no saved Attunement.";
        var attuned = UnpackIndependentCopy(source);
        var baseline = UnpackIndependentCopy(source);
        baseline.individualID = 0; // No Attunement record; all rolled bytes remain identical.
        baseline.RefreshIDAndValues();
        var result = new StringBuilder($"Item {source.individualID}: saved multiplier {state.Multiplier:F3}\n");
        for (int i = 0; i < attuned.affixes.Count; i++)
        {
            var affix = attuned.affixes[i];
            result.AppendLine($"Affix {affix.affixId}: factor={state.AffixMultiplier(affix.affixId, affix.affixTier, affix.affixRoll):F3}");
            result.AppendLine("  baseline tooltip: " + AffixText(baseline, baseline.affixes[i]));
            result.AppendLine("  attuned tooltip:  " + AffixText(attuned, affix));
            var normal = new Il2CppSystem.Collections.Generic.List<Stats.Stat>();
            var scaled = new Il2CppSystem.Collections.Generic.List<Stats.Stat>();
            baseline.AddAffixValueToStatList(baseline.affixes[i], false, ref normal);
            attuned.AddAffixValueToStatList(affix, false, ref scaled);
            for (int n = 0; n < Math.Min(normal.Count, scaled.Count); n++)
                result.AppendLine($"  native {normal[n].property}: added {normal[n].addedValue:G6} -> {scaled[n].addedValue:G6}; increased {normal[n].increasedValue:G6} -> {scaled[n].increasedValue:G6}");
        }
        result.AppendLine("Only in-memory copies were calculated. The item, orb count and saved records were unchanged.");
        return result.ToString();
    }

    private static string AffixText(ItemDataUnpacked item, ItemAffix affix) =>
        TooltipItemManager.FormatAffix(item, affix, TooltipMode.DefaultInfo, TooltipItemManager.SlotType.CRAFTING,
            false, -1, -1, false, false, ValueRangeFormat.None, true, false);

    // Register only for the duration of the synchronous in-memory round trip. Never mutate the Forge item.
    public static string ProbeMemory()
    {
        AffixList catalog = AffixList.get();
        if (catalog.IsNullOrDestroyed()) return "Affix catalog is not loaded.";

        ItemData source = Items_AdvancedForgeWindow.InspectorItem ?? Items_Encapsulation.ForgeItem();
        if (source.IsNullOrDestroyed()) return "Place an ordinary test item in either Forge first.";
        if (CraftProtection.IsImmutable(source)) return "Use a regular uncorrupted test item.";

        var result = new StringBuilder();
        int firstId = catalog.getHighestAffixID() + 1;
        if (firstId <= 0 || firstId >= ushort.MaxValue) return "No candidate ID range for a two-affix probe.";

        foreach (AffixList.AffixType side in new[] { AffixList.AffixType.PREFIX, AffixList.AffixType.SUFFIX })
        {
            int id = firstId++;
            var placeholder = new AffixList.MultiAffix
            {
                affixId = id,
                affixName = "Obscured Memory",
                affixDisplayName = "Obscured Memory",
                weighting = 0f,
                type = side,
                affixProperties = new Il2CppSystem.Collections.Generic.List<AffixList.AffixProperty>(),
                tiers = new Il2CppSystem.Collections.Generic.List<AffixList.Tier>(),
            };
            placeholder.tiers.Add(new AffixList.Tier());
            var original = catalog.multiAffixes;

            try
            {
                var temporary = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<AffixList.MultiAffix>(original.Length + 1);
                for (int i = 0; i < original.Length; i++) temporary[i] = original[i];
                temporary[original.Length] = placeholder;
                catalog.multiAffixes = temporary;
                catalog.RefreshAllAffixesList();
                var registered = catalog.GetAffix((ushort)id);
                if (registered == null || registered.Pointer != placeholder.Pointer)
                {
                    result.AppendLine($"{side}: catalog did not resolve temporary ID {id}.");
                    continue;
                }

                var copy = UnpackIndependentCopy(source);
                ItemAffix replace = copy.affixes.ToArray().FirstOrDefault(a =>
                    a != null && !a.IsSealed && a.affixType == side && !Items_Encapsulate.IsEncapsulated(source, a));
                if (replace != null) copy.affixes.Remove(replace);
                else if (copy.affixes.Count >= LastEpoch_Hud.Scripts.Core.Crafting.RecombinatorMath.MaxAffixes)
                {
                    result.AppendLine($"{side}: no open or replaceable slot in test item.");
                    continue;
                }
                copy.affixes.Add(new ItemAffix((ushort)id, 0, 0, copy.itemType, SealedAffixType.None));
                copy.sockets = (byte)copy.affixes.Count;
                copy.RollIndividualID();
                copy.RefreshIDAndValues();

                var restored = UnpackIndependentCopy(copy);
                var loaded = restored.affixes.ToArray().FirstOrDefault(a => a != null && a.affixId == id);
                result.AppendLine(loaded != null && loaded.affixType == side
                    ? $"{side}: ID {id} survived native pack/unpack in its slot."
                    : $"{side}: ID {id} was lost or changed during native pack/unpack.");
            }
            catch (Exception ex)
            {
                result.AppendLine($"{side}: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                catalog.multiAffixes = original;
                catalog.RefreshAllAffixesList();
            }
        }
        result.AppendLine("Temporary catalog entries removed. The probe used copies and did not write a save.");
        return result.ToString();
    }

    private static ItemDataUnpacked UnpackIndependentCopy(ItemData item)
    {
        var packed = item.GetID();
        var bytes = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>(packed.Length);
        for (int i = 0; i < packed.Length; i++) bytes[i] = packed[i];
        var copy = new ItemDataUnpacked();
        copy.RebuildFromID(bytes);
        return copy;
    }

    private static string Definition(AffixList.Affix definition) =>
        $"id={definition.affixId} side={definition.type} family={definition.specialAffixType} "
        + $"tiers={definition.tiers?.Count ?? 0} weight={definition.weighting}";
}
