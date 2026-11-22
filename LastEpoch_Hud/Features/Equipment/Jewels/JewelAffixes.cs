using System;
using System.Collections.Generic;
using System.IO;
using LastEpoch_Hud.Scripts.Core.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LastEpoch_Hud.Scripts.Core.Jewels;

namespace LastEpoch_Hud.Scripts.Mods.Items.Jewels;

/// <summary>Registers independent, serializable jewel affixes without modifying native idol definitions.</summary>
internal static class JewelAffixes
{
    private static AffixList registeredCatalog;
    private static readonly Dictionary<int, AffixList.Affix> definitions = new();
    internal static bool Registered => registeredCatalog != null && definitions.Count != 0;
    internal static IReadOnlyCollection<AffixList.Affix> Definitions => definitions.Values;
    internal static bool IsJewelAffix(int id) => definitions.ContainsKey(id);
    internal static int MinimumTier(int id) => JewelAffixRules.MinimumTier(id);

    // Roll step per affix from Data/JewelAffixes.csv (0 or missing: continuous).
    private static Dictionary<int, float> steps = new();
    private static readonly Dictionary<int, string> idolTypes = new();
    private static readonly HashSet<int> corruptionCopies = new();
    private static readonly Dictionary<int, float> corruptionWeights = new();
    internal static bool IsCorruptionCopy(int id) => corruptionCopies.Contains(id);
    internal static float StepOf(int id) => steps.TryGetValue(id, out float step) ? step : 0f;
    internal static AffixList.Affix Get(int id) => definitions.TryGetValue(id, out var value) ? value : null;
    internal static AffixList.Affix FindById(int id) => Get(id);
    internal static IEnumerable<AffixList.Affix> RollableDefinitions(int maxTier = 7) =>
        definitions.Values.Where(x => !IsCorruptionCopy(x.affixId) && MinimumTier(x.affixId) <= maxTier && x.weighting > 0);

    /// <summary>Call after the base catalog and subtypes 0..3 exist. Safe to retry before they exist.</summary>
    internal static void EnsureRegistered()
    {
        if (Registered) return;
        var items = ItemList.get();
        var catalog = AffixList.get();
        if (items == null || catalog == null || catalog.singleAffixes == null || catalog.multiAffixes == null) return;
        var jewelBase = items.EquippableItems?.FirstOrDefault(x => x != null && x.baseTypeID == JewelAffixRules.BaseType);
        if (jewelBase?.subItems == null || Enumerable.Range(0, 4).Any(id => !jewelBase.subItems.ToArray().Any(s => s != null && s.subTypeID == id))) return;
        if (registeredCatalog == catalog && Registered) return;

        // Stage everything before publishing, so a collision cannot leave half a catalog registered.
        var singles = catalog.singleAffixes.ToArray().ToList();
        var multis = catalog.multiAffixes.ToArray().ToList();
        var staged = new Dictionary<int, AffixList.Affix>();
        corruptionCopies.Clear();
        corruptionWeights.Clear();
        WriteNativeCorruptionPools(singles.Cast<AffixList.Affix>().Concat(multis));
        foreach (var source in singles.Cast<AffixList.Affix>().Concat(multis).ToArray())
        {
            if (source == null || ((int)source.classSpecificity & ~1) != 0) continue;
            // RollsOn has only Equipment/Idols in this game build. Also inspect the actual
            // applicability list so mixed equipment/idol definitions are not lost.
            if (source.rollsOn != AffixList.RollsOn.Idols
                && (source.canRollOn == null || !source.canRollOn.ToArray().Any(type => ItemList.isIdol(type)))) continue;
            if (source.tiers == null || source.tiers.Count == 0)
                throw new InvalidOperationException("Idol affix has no ranges: " + source.affixId);
            var single = source.TryCast<AffixList.SingleAffix>();
            AffixList.Affix copy;
            if (single != null)
            {
                var target = new AffixList.SingleAffix();
                CopyProperties(single, target, typeof(AffixList.SingleAffix));
                singles.Add(target);
                copy = target;
            }
            else
            {
                var target = new AffixList.MultiAffix();
                CopyProperties(source.TryCast<AffixList.MultiAffix>(), target, typeof(AffixList.MultiAffix));
                var properties = new Il2CppSystem.Collections.Generic.List<AffixList.AffixProperty>();
                foreach (var property in target.affixProperties)
                {
                    var cloned = new AffixList.AffixProperty();
                    CopyProperties(property, cloned, typeof(AffixList.AffixProperty));
                    properties.Add(cloned);
                }
                target.affixProperties = properties;
                multis.Add(target);
                copy = target;
            }
            // Which idols the game rolls this affix on, before the copy is restricted to Jewels.
            idolTypes[JewelAffixRules.InheritedId(source.affixId)] = source.canRollOn == null ? ""
                : string.Join("|", source.canRollOn.ToArray().Select(type => type.ToString()));
            if (source.specialAffixType == AffixList.SpecialAffixType.Corrupted)
            {
                corruptionCopies.Add(JewelAffixRules.InheritedId(source.affixId));
                corruptionWeights[JewelAffixRules.InheritedId(source.affixId)] = source.weighting;
            }
            Configure(copy, JewelAffixRules.InheritedId(source.affixId));
            if (IsCorruptionCopy(copy.affixId)) copy.specialAffixType = AffixList.SpecialAffixType.Corrupted;
            // Idol T1 is the sole baseline, including every secondary roll. Never use
            // the idol's higher tiers when constructing jewel T2..T7.
            copy.tiers = CopiedTiers(source.tiers[0]);
            if (IsCorruptionCopy(copy.affixId))
                foreach (var tier in copy.tiers)
                {
                    var primary = JewelAffixRules.CorruptionRange(tier.minRoll, tier.maxRoll, copy.affixId);
                    tier.minRoll = primary.Min;
                    tier.maxRoll = primary.Max;
                    if (tier.extraRolls == null) continue;
                    for (int index = 0; index < tier.extraRolls.Count; index++)
                    {
                        var extra = tier.extraRolls[index];
                        var secondary = JewelAffixRules.CorruptionRange(extra.minRoll, extra.maxRoll, copy.affixId, index + 1);
                        extra.minRoll = secondary.Min;
                        extra.maxRoll = secondary.Max;
                    }
                }
            if (source.affixId == 830)
                Main.logger_instance?.Msg("Jewel stun idol baseline: " + source.tiers.Count + " native tiers; T1 "
                    + source.tiers[0].minRoll + " to " + source.tiers[0].maxRoll
                    + "; T1 extra " + string.Join(" | ", source.tiers[0].extraRolls.ToArray().Select(r => r.minRoll + " to " + r.maxRoll)));
            if (source.affixId == 830)
                Main.logger_instance?.Msg("Jewel stun native standard effect modifier: " + source.standardAffixEffectModifier
                    + "; jewel standard effect modifier: " + copy.standardAffixEffectModifier);
            staged.Add(copy.affixId, copy);
        }
        foreach (var affix in ExclusiveAffixes())
        {
            staged.Add(affix.affixId, affix);
            singles.Add(affix);
        }
        ApplyTable(staged, singles);
        var oldIds = catalog.singleAffixes.Where(x => x != null).Select(x => x.affixId)
            .Concat(catalog.multiAffixes.Where(x => x != null).Select(x => x.affixId)).ToHashSet();
        foreach (int id in staged.Keys)
        {
            if (id > JewelAffixRules.MaximumPackedAffixId)
                throw new InvalidOperationException("Jewel affix ID cannot survive native serialization: " + id);
            if (oldIds.Contains(id)) throw new InvalidOperationException("Jewel affix ID collision: " + id);
        }
        var previousSingles = catalog.singleAffixes;
        var previousMultis = catalog.multiAffixes;
        try
        {
            catalog.singleAffixes = new Il2CppReferenceArray<AffixList.SingleAffix>(singles.OrderBy(x => x?.affixId ?? int.MaxValue).ToArray());
            catalog.multiAffixes = new Il2CppReferenceArray<AffixList.MultiAffix>(multis.OrderBy(x => x?.affixId ?? int.MaxValue).ToArray());
            catalog.RefreshAllAffixesList();
            foreach (var pair in staged)
                if (catalog.GetAffix(pair.Key)?.Pointer != pair.Value.Pointer)
                    throw new InvalidOperationException("Native jewel affix lookup failed: " + pair.Key);
        }
        catch
        {
            catalog.singleAffixes = previousSingles;
            catalog.multiAffixes = previousMultis;
            catalog.RefreshAllAffixesList();
            throw;
        }
        definitions.Clear();
        foreach (var pair in staged) definitions.Add(pair.Key, pair.Value);
        registeredCatalog = catalog;
        Main.logger_instance?.Msg($"Jewels: registered {staged.Count} exclusive affixes (including {staged.Count - 35} idol copies).");
    }

    // ---------------------------------------------------------------- Data/JewelAffixes.csv (embedded at compile time)

    // Only used while Data/JewelAffixes.csv is missing from the source: the defaults are written here to be copied in.
    private static string GeneratedPath => Path.Combine(Directory.GetCurrentDirectory(), "Mods", Main.mod_name, "JewelAffixes.generated.csv");

    private static JewelAffixRow ToRow(AffixList.Affix affix)
    {
        var single = affix.TryCast<AffixList.SingleAffix>();
        var row = new JewelAffixRow
        {
            Id = affix.affixId,
            Origin = affix.affixId >= JewelAffixRules.ExclusiveIdStart ? "Exclusive" : "Inherited",
            SourceId = affix.affixId >= JewelAffixRules.ExclusiveIdStart ? 0 : affix.affixId - JewelAffixRules.InheritedIdOffset,
            Name = string.IsNullOrEmpty(affix.affixDisplayName) ? affix.affixName : affix.affixDisplayName,
            Title = affix.affixTitle ?? "",
            TitleType = affix.titleType.ToString(),
            Side = affix.type == AffixList.AffixType.SUFFIX ? "Suffix" : "Prefix",
            Property = single == null ? "" : single.property.ToString(),
            ModType = single == null ? "" : single.modifierType.ToString(),
            Tags = single == null ? "" : single.tags.ToString(),
            Weight = (int)Math.Round(affix.weighting),
            MinTier = MinimumTier(affix.affixId),
            Step = StepOf(affix.affixId),
        };
        for (int tier = 0; tier < JewelAffixRow.TierCount && tier < affix.tiers.Count; tier++)
        {
            row.Min[tier] = affix.tiers[tier].minRoll;
            row.Max[tier] = affix.tiers[tier].maxRoll;
        }
        return row;
    }

    /// <summary>
    /// Applies Data/JewelAffixes.csv over the built-in defaults: weights, minimum tiers and the T1..T7 ranges, plus any
    /// new Exclusive rows. The file is part of the source and is embedded when the mod is built.
    /// </summary>
    private static void ApplyTable(Dictionary<int, AffixList.Affix> staged, List<AffixList.SingleAffix> singles)
    {
        JewelAffixRules.SetMinimumTierOverrides(null);
        try
        {
            string text = EmbeddedData.Read("JewelAffixes.csv");
            if (text == null)
            {
                // No table in the source yet: use the defaults, and write them out so they can be added to Data/.
                Directory.CreateDirectory(Path.GetDirectoryName(GeneratedPath));
                File.WriteAllText(GeneratedPath, JewelAffixTable.Write(staged.Values.OrderBy(x => x.affixId).Select(ToRow)));
                Main.logger_instance?.Msg("Jewel affixes: Data/JewelAffixes.csv is not in the mod. Wrote the defaults to " + GeneratedPath
                    + ". Copy it to LastEpoch_Hud/Data/JewelAffixes.csv and rebuild.");
                return;
            }
            var errors = new List<string>();
            var rows = JewelAffixTable.Parse(text, errors);
            var minimums = new Dictionary<int, int>();
            var nextSteps = new Dictionary<int, float>();
            int changed = 0, added = 0;
            foreach (var row in rows)
            {
                if (!staged.TryGetValue(row.Id, out var affix))
                {
                    affix = CreateFromRow(row, errors);
                    if (affix == null) continue;
                    staged.Add(row.Id, affix);
                    singles.Add((AffixList.SingleAffix)affix);
                    added++;
                }
                else changed++;
                affix.weighting = IsCorruptionCopy(row.Id) ? corruptionWeights[row.Id] : row.Weight;
                if (!row.IsInherited && row.Name.Length != 0) { affix.affixName = row.Name; affix.affixDisplayName = row.Name; }
                if (row.Title.Length != 0) affix.affixTitle = row.Title;
                if (row.TitleType.Length != 0 && Enum.TryParse(row.TitleType, true, out AffixList.AffixTitleType titleType)) affix.titleType = titleType;
                minimums[row.Id] = row.MinTier;
                if (row.Step > 0) nextSteps[row.Id] = row.Step;
                ApplyRanges(affix, row);
            }
            // The table is the whole list: an affix with no row is switched off (existing items keep it).
            if (rows.Count > 0)
            {
                var listed = new HashSet<int>(rows.Select(r => r.Id));
                foreach (var pair in staged)
                    if (!listed.Contains(pair.Key)) pair.Value.weighting = 0;
            }
            JewelAffixRules.SetMinimumTierOverrides(minimums);
            steps = nextSteps;
            // Inherited rows with no Title keep the game's own. Write the current table, native titles included, so
            // they can be copied into Data/JewelAffixes.csv and edited.
            if (rows.Exists(r => r.IsInherited && r.Title.Length == 0))
            {
                string path = Path.Combine(Path.GetDirectoryName(GeneratedPath), "JewelAffixes.export.csv");
                File.WriteAllText(path, JewelAffixTable.Write(staged.Values.OrderBy(x => x.affixId).Select(ToRow)));
                Main.logger_instance?.Msg("Jewel affixes: wrote the current table with the game's own titles to " + path);
            }
            WriteIdolSizes(staged);
            foreach (string error in errors) Main.logger_instance?.Warning("Jewel affixes (Data/JewelAffixes.csv): " + error);
            Main.logger_instance?.Msg("Jewel affixes: loaded the table (" + changed + " changed, " + added + " added, " + errors.Count + " problems).");
        }
        catch (Exception ex)
        {
            // A broken table must never stop the Jewels registering: fall back to the built-in definitions.
            JewelAffixRules.SetMinimumTierOverrides(null);
            ErrorLog.Report(ex, "Jewel affix table");
        }
    }

    // Dev aid: which idol sizes the game rolls each copied Idol affix on (JewelAffixes.idols.csv, next to the other exports).
    private static void WriteNativeCorruptionPools(IEnumerable<AffixList.Affix> nativeAffixes)
    {
        try
        {
            var text = new System.Text.StringBuilder("NativeId,Name,Title,IdolTypes,AllowedOnRing,ClassSpecificity,Tier,RawMin,RawMax,RawSecondaryRanges,StandardAffixEffectModifier,Weight\r\n");
            var idolNames = new List<string>();
            foreach (var affix in nativeAffixes.OrderBy(affix => affix?.affixId ?? int.MaxValue))
            {
                if (affix == null || affix.specialAffixType != AffixList.SpecialAffixType.Corrupted) continue;
                var idols = affix.canRollOn == null ? Array.Empty<EquipmentType>()
                    : affix.canRollOn.ToArray().Where(type => ItemList.isIdol(type)).ToArray();
                bool ring = affix.CanRollOn((int)EquipmentType.RING, 0, ItemList.ClassRequirement.Any);
                if (idols.Length == 0 && !ring) continue;
                if (idols.Length > 0) idolNames.Add(affix.affixId + " " + affix.affixName);
                for (int tier = 0; affix.tiers != null && tier < affix.tiers.Count; tier++)
                {
                    var range = affix.tiers[tier];
                    string secondary = range.extraRolls == null ? "" : string.Join(" | ", range.extraRolls.ToArray()
                        .Select(roll => roll.minRoll.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + " to " + roll.maxRoll.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    text.Append(affix.affixId).Append(',').Append(CsvText.Quote(affix.affixName ?? ""))
                        .Append(',').Append(CsvText.Quote(affix.affixTitle ?? ""))
                        .Append(',').Append(CsvText.Quote(string.Join("|", idols.Select(type => type.ToString()))))
                        .Append(',').Append(ring).Append(',').Append(affix.classSpecificity).Append(',').Append(tier + 1)
                        .Append(',').Append(range.minRoll.ToString(System.Globalization.CultureInfo.InvariantCulture))
                        .Append(',').Append(range.maxRoll.ToString(System.Globalization.CultureInfo.InvariantCulture))
                        .Append(',').Append(CsvText.Quote(secondary))
                        .Append(',').Append(affix.standardAffixEffectModifier.ToString(System.Globalization.CultureInfo.InvariantCulture))
                        .Append(',').Append(affix.weighting.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(GeneratedPath));
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(GeneratedPath), "JewelAffixes.native-corruption.csv"), text.ToString());
            Main.logger_instance?.Msg("Native idol corruption pool: " + string.Join(" | ", idolNames));
        }
        catch (Exception ex) { Main.logger_instance?.Warning("Native corruption pool export: " + ex.Message); }
    }

    private static void WriteIdolSizes(Dictionary<int, AffixList.Affix> staged)
    {
        try
        {
            var text = new System.Text.StringBuilder("Id,Name,Title,Sizes,IdolTypes\r\n");
            foreach (var pair in staged.OrderBy(p => p.Key))
            {
                if (!idolTypes.TryGetValue(pair.Key, out string types)) continue;
                var shapes = types.Split(new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.StartsWith("IDOL_") ? t.Substring(5).Replace("_ETERRA", "").Replace("_LAGON", "") : t).Distinct();
                string name = (string.IsNullOrEmpty(pair.Value.affixDisplayName) ? pair.Value.affixName : pair.Value.affixDisplayName) ?? "";
                text.Append(pair.Key).Append(',').Append(CsvText.Quote(name)).Append(',').Append(CsvText.Quote(pair.Value.affixTitle ?? ""))
                    .Append(',').Append(CsvText.Quote(string.Join(" ", shapes))).Append(',').Append(CsvText.Quote(types)).Append("\r\n");
            }
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(GeneratedPath), "JewelAffixes.idols.csv"), text.ToString());
            var corrupted = staged.Values.Where(affix => IsCorruptionCopy(affix.affixId)).OrderBy(affix => affix.affixId).ToList();
            var corruptionReport = new System.Text.StringBuilder("JewelId,NativeId,InternalName,DisplayName,NativeWeight,Enabled\r\n");
            foreach (var affix in corrupted)
                corruptionReport.Append(affix.affixId).Append(',').Append(affix.affixId - JewelAffixRules.InheritedIdOffset)
                    .Append(',').Append(CsvText.Quote(affix.affixName ?? ""))
                    .Append(',').Append(CsvText.Quote(affix.affixDisplayName ?? ""))
                    .Append(',').Append(affix.weighting.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Append(',').Append(affix.weighting > 0).Append("\r\n");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(GeneratedPath), "JewelAffixes.corruption.csv"), corruptionReport.ToString());
            Main.logger_instance?.Msg("Jewel-only corruption pool (excluded from ordinary rolls): "
                + string.Join(" | ", corrupted.Select(affix => affix.affixId + " " + affix.affixName)));
        }
        catch (Exception ex) { Main.logger_instance?.Warning("Jewel affixes: could not write the idol sizes (" + ex.Message + ")"); }
    }

    private static void ApplyRanges(AffixList.Affix affix, JewelAffixRow row)
    {
        for (int tier = 0; tier < JewelAffixRow.TierCount && tier < affix.tiers.Count; tier++)
        {
            var range = affix.tiers[tier];
            float oldMax = range.maxRoll;
            float ratio = oldMax > 0 ? row.Max[tier] / oldMax : 1f;
            range.minRoll = row.Min[tier];
            range.maxRoll = row.Max[tier];
            // Extra rolls of a multi-property affix keep their proportion to the main roll.
            if (range.extraRolls != null && range.extraRolls.Count > 0 && ratio != 1f)
            {
                var scaled = new Il2CppSystem.Collections.Generic.List<AffixList.RollPair>();
                foreach (var extra in range.extraRolls) scaled.Add(new AffixList.RollPair(extra.minRoll * ratio, extra.maxRoll * ratio));
                range.extraRolls = scaled;
            }
        }
    }

    private static AffixList.Affix CreateFromRow(JewelAffixRow row, List<string> errors)
    {
        if (row.IsInherited)
        {
            errors.Add("Id " + row.Id + " is Inherited but no Idol affix " + row.SourceId + " exists, so it was skipped.");
            return null;
        }
        if (row.Id < JewelAffixRules.ExclusiveIdStart || row.Id > JewelAffixRules.MaximumPackedAffixId)
        {
            errors.Add("Id " + row.Id + ": new Exclusive affixes need an Id from " + JewelAffixRules.ExclusiveIdStart + " to " + JewelAffixRules.MaximumPackedAffixId + ".");
            return null;
        }
        AT tags = AT.None;
        if (!Enum.TryParse(row.Property, true, out SP property) || !Enum.TryParse(row.ModType, true, out BaseStats.ModType type)
            || (row.Tags.Length != 0 && !Enum.TryParse(row.Tags, true, out tags)))
        {
            errors.Add("Id " + row.Id + ": Property, ModType or Tags is not a name the game knows, so it was skipped.");
            return null;
        }
        string name = row.Name.Length == 0 ? "Jewel " + row.Id : row.Name;
        var affix = new AffixList.SingleAffix
        {
            affixName = name, affixDisplayName = name, affixTitle = row.Title.Length == 0 ? name : row.Title,
            property = property, modifierType = type, tags = tags, weighting = row.Weight,
            type = string.Equals(row.Side, "Suffix", StringComparison.OrdinalIgnoreCase) ? AffixList.AffixType.SUFFIX : AffixList.AffixType.PREFIX,
            useGeneratedNameForDisplayName = property != SP.None, standardAffixEffectModifier = 0,
        };
        Configure(affix, row.Id);
        affix.tiers = ExclusiveTiers(0, 0, 0, 0, 0, 0);
        return affix;
    }

    // Copy all writable native metadata, including name morphology and tooltip metadata. Replace mutable
    // ranges, reroll lists, equipment applicability, and multi-properties with jewel-owned objects below.
    private static void CopyProperties(object source, object target, Type type)
    {
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                property.SetValue(target, property.GetValue(source));
    }

    private static void Configure(AffixList.Affix affix, int id)
    {
        affix.affixId = id;
        affix.toCopyFrom = 0;
        affix.rollsOn = AffixList.RollsOn.Equipment;
        affix.classSpecificity = AffixList.ClassSpecificity.NonSpecific;
        affix.specialAffixType = AffixList.SpecialAffixType.Standard;
        affix.uniqueId = 0;
        // Native getModifier divides by (1 + standardAffixEffectModifier).
        // Copied idol normalization must not amplify our already-scaled CSV ranges.
        affix.standardAffixEffectModifier = 0;
        affix.canRollOn = new Il2CppSystem.Collections.Generic.List<EquipmentType>();
        affix.canRollOn.Add((EquipmentType)JewelAffixRules.BaseType);
        affix.specificRerollChances = new Il2CppSystem.Collections.Generic.List<AffixList.SpecificReroll>();
        affix.convertOnIncompatibleItemType = false;
        affix.affixIDToConvertTo = 0;
        affix.t6Compatibility = AffixList.T6Compatibility.Normal;
    }

    // Every tier starts as an exact copy of the Idol affix's own range. Data/JewelAffixes.csv sets the real per-tier ranges.
    private static Il2CppSystem.Collections.Generic.List<AffixList.Tier> CopiedTiers(AffixList.Tier baseline)
    {
        var tiers = new Il2CppSystem.Collections.Generic.List<AffixList.Tier>();
        for (int tier = 1; tier <= 7; tier++)
        {
            var range = new AffixList.Tier { minRoll = baseline.minRoll, maxRoll = baseline.maxRoll,
                extraRolls = new Il2CppSystem.Collections.Generic.List<AffixList.RollPair>() };
            if (baseline.extraRolls != null)
                foreach (var extra in baseline.extraRolls)
                    range.extraRolls.Add(new AffixList.RollPair(extra.minRoll, extra.maxRoll));
            tiers.Add(range);
        }
        return tiers;
    }

    private static AffixList.SingleAffix Exclusive(int index, string name, SP property, BaseStats.ModType type,
        float min, float max, int weight, AT tags = AT.None, bool suffix = false)
    {
        var affix = new AffixList.SingleAffix { affixName = name, affixDisplayName = name, affixTitle = name,
            property = property, modifierType = type, tags = tags, weighting = weight,
            type = suffix ? AffixList.AffixType.SUFFIX : AffixList.AffixType.PREFIX,
            useGeneratedNameForDisplayName = property != SP.None, standardAffixEffectModifier = 0 };
        Configure(affix, JewelAffixRules.ExclusiveIdStart + index);
        affix.tiers = ExclusiveTiers(min, max, min, max, min, max);
        return affix;
    }

    // Exclusive T1..T5 use linear per-tier values: T1=low, T5=high (min=max).
    // T6/T7 retain the exact requested roll ranges. Idol multipliers never apply here.
    private static Il2CppSystem.Collections.Generic.List<AffixList.Tier> ExclusiveTiers(
        float low, float high, float t6min, float t6max, float t7min, float t7max)
    {
        var result = new Il2CppSystem.Collections.Generic.List<AffixList.Tier>();
        for (int tier = 1; tier <= 7; tier++)
        {
            var range = JewelAffixRules.ExclusiveRange(tier, low, high, t6min, t6max, t7min, t7max);
            result.Add(new AffixList.Tier { minRoll = range.Min, maxRoll = range.Max,
                extraRolls = new Il2CppSystem.Collections.Generic.List<AffixList.RollPair>() });
        }
        return result;
    }

    private static AffixList.SingleAffix WithRanges(AffixList.SingleAffix affix,
        float low, float high, float t6min, float t6max, float t7min, float t7max)
    {
        affix.tiers = ExclusiveTiers(low, high, t6min, t6max, t7min, t7max);
        return affix;
    }

    // "DamageTakenAsFire" -> "Damage Taken As Fire", "FIRE" -> "Fire".
    private static string Nice(string name) => System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");
    private static string Capitalized(string name) => name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name.Substring(1).ToLowerInvariant();

    private static IEnumerable<AffixList.SingleAffix> ExclusiveAffixes()
    {
        var attributes = new[] { SP.AllAttributes, SP.Strength, SP.Dexterity, SP.Intelligence, SP.Attunement, SP.Vitality };
        for (int i = 0; i < attributes.Length; i++)
        {
            yield return WithRanges(Exclusive(i * 2, Nice(attributes[i].ToString()), attributes[i], BaseStats.ModType.ADDED,
                0, 0, i == 0 ? 10 : 100), i == 0 ? 1 : 2, i == 0 ? 5 : 8,
                i == 0 ? 6 : 8, i == 0 ? 8 : 12, i == 0 ? 10 : 12, i == 0 ? 12 : 16);
            yield return WithRanges(Exclusive(i * 2 + 1, "Increased " + Nice(attributes[i].ToString()), attributes[i], BaseStats.ModType.INCREASED,
                0, 0, i == 0 ? 10 : 100), i == 0 ? .02f : .03f, i == 0 ? .07f : .08f,
                i == 0 ? .08f : .10f, i == 0 ? .10f : .12f, i == 0 ? .10f : .12f, i == 0 ? .12f : .15f);
        }
        var manaShield = Exclusive(12, "Mana Before Health", SP.ManaBeforeHealthPercent, BaseStats.ModType.ADDED, 0, 0, 100, suffix: true);
        for (int i = 0; i < 7; i++) manaShield.tiers[i].minRoll = manaShield.tiers[i].maxRoll = i < 5 ? .10f : i == 5 ? .15f : .20f;
        yield return manaShield;
        yield return WithRanges(Exclusive(13, "Melee Attack Speed", SP.AttackSpeed, BaseStats.ModType.INCREASED, 0, 0, 1000, AT.Melee), .04f, .15f, .16f, .20f, .21f, .24f);
        yield return WithRanges(Exclusive(14, "Cast Speed", SP.CastSpeed, BaseStats.ModType.INCREASED, 0, 0, 1000), .04f, .15f, .16f, .20f, .21f, .24f);
        yield return WithRanges(Exclusive(15, "More Movement Speed", SP.Movespeed, BaseStats.ModType.MORE, 0, 0, 100, suffix: true), .01f, .03f, .04f, .06f, .07f, .10f);
        yield return WithRanges(Exclusive(16, "Throwing Attack Speed", SP.AttackSpeed, BaseStats.ModType.INCREASED, 0, 0, 1000, AT.Throwing), .05f, .20f, .22f, .26f, .28f, .32f);
        var tags = new[] { AT.None, AT.Spell, AT.Melee, AT.Throwing };
        var leech = new[,] { { .004f, .018f, .02f, .024f, .03f, .036f }, { .005f, .02f, .023f, .031f, .032f, .043f },
            { .012f, .024f, .026f, .036f, .042f, .05f }, { .006f, .022f, .024f, .034f, .034f, .045f } };
        for (int i = 0; i < tags.Length; i++)
            yield return WithRanges(Exclusive(20 + i, (i == 0 ? "Damage" : tags[i] + " Damage") + " Mana Leech",
                SP.None, BaseStats.ModType.ADDED, 0, 0, 100, tags[i], true), leech[i, 0], leech[i, 1], leech[i, 2], leech[i, 3], leech[i, 4], leech[i, 5]);
        var taken = new[] { SP.DamageTakenAsPhysical, SP.DamageTakenAsFire, SP.DamageTakenAsCold,
            SP.DamageTakenAsLightning, SP.DamageTakenAsNecrotic, SP.DamageTakenAsVoid, SP.DamageTakenAsPoison };
        for (int i = 0; i < 7; i++)
        {
            var extra = WithRanges(Exclusive(30 + i, "Damage As Extra " + Capitalized(((DamageType)i).ToString()) + " Damage", SP.None, BaseStats.ModType.ADDED, 0, 0, 10), 0, 0, .05f, .07f, .10f, .12f);
            var conversion = WithRanges(Exclusive(40 + i, Nice(taken[i].ToString()), taken[i], BaseStats.ModType.ADDED, 0, 0, 10, suffix: true), 0, 0, .03f, .06f, .08f, .10f);
            // Native tier indices remain 0..6. Unavailable tiers have no effect; generation must filter
            // using MinimumTier. Never promote a crafted T1 affix into a free exalted affix.
            for (int tier = 0; tier < 5; tier++)
            {
                extra.tiers[tier].minRoll = extra.tiers[tier].maxRoll = 0;
                conversion.tiers[tier].minRoll = conversion.tiers[tier].maxRoll = 0;
            }
            yield return extra;
            yield return conversion;
        }
    }

    /// <summary>Weighted demo rolls, unrestricted sides, no duplicates, and legal display tiers.</summary>
    internal static Il2CppSystem.Collections.Generic.List<ItemAffix> CreateAffixes(Random random, int count = 4, int maxTier = 7)
    {
        if (random == null) throw new ArgumentNullException(nameof(random));
        if (count < 0 || count > 6 || maxTier < 1 || maxTier > 7) throw new ArgumentOutOfRangeException();
        EnsureRegistered();
        if (!Registered) throw new InvalidOperationException("The jewel base and affix catalogs are not ready.");
        var result = new Il2CppSystem.Collections.Generic.List<ItemAffix>();
        var candidates = RollableDefinitions(maxTier).ToList();
        while (result.Count < count && candidates.Count != 0)
        {
            double roll = random.NextDouble() * candidates.Sum(x => (double)x.weighting);
            var pick = candidates[candidates.Count - 1];
            foreach (var candidate in candidates) { roll -= candidate.weighting; if (roll < 0) { pick = candidate; break; } }
            result.Add(new ItemAffix((ushort)pick.affixId, (byte)(random.Next(MinimumTier(pick.affixId), maxTier + 1) - 1),
                (byte)random.Next(256), JewelAffixRules.BaseType, SealedAffixType.None));
            candidates.RemoveAll(x => x.affixId == pick.affixId);
        }
        return result;
    }

    // Prevent ordinary native equipment/idol affixes leaking onto jewels, and jewel copies leaking off.
    [HarmonyPatch]
    private static class Applicability
    {
        private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(AffixList.Affix))
            .Where(m => m.Name == nameof(AffixList.Affix.CanRollOnItemType));
        private static bool Prefix(AffixList.Affix __instance, EquipmentType __0, ref bool __result)
        {
            bool jewel = (int)__0 == JewelAffixRules.BaseType;
            bool affix = IsJewelAffix(__instance.affixId);
            if (!jewel && !affix) return true;
            __result = jewel && affix;
            return false;
        }
    }

    // Never set: the native RollAffixes generator cannot be patched (a patch on it crashed the game), so Jewels
    // generated by the game never get Exalted-tier affixes; the mod's own Jewel creation sets tiers directly.
    private static bool canGenerateExalted;

    // Native Tier stores only ranges, not weights. Filter affix weights while ordinary
    // tiers are being generated, then constrain the native tier roll to indices 5/6.

    [HarmonyPatch(typeof(AffixList.Affix), nameof(AffixList.Affix.GetWeighting))]
    private static class NativeTierWeight
    {
        private static void Postfix(AffixList.Affix __instance, ref float __result)
        {
            if (IsJewelAffix(__instance.affixId) && MinimumTier(__instance.affixId) == 6 && !canGenerateExalted) __result = 0;
        }
    }

    [HarmonyPatch(typeof(GenerateItems), nameof(GenerateItems.ApplyRollModifiersToAffixWeight))]
    private static class NativeModifiedTierWeight
    {
        private static void Postfix(AffixList.Affix __1, ref float __result)
        {
            if (__1 != null && IsJewelAffix(__1.affixId) && MinimumTier(__1.affixId) == 6 && !canGenerateExalted) __result = 0;
        }
    }

    [HarmonyPatch(typeof(GenerateItems), nameof(GenerateItems.RollTier))]
    private static class NativeTierFloor
    {
        private static void Postfix(ItemData __0, AffixList.Affix __1, ref int __result)
        {
            if (__0 == null || __0.itemType != JewelAffixRules.BaseType || __1 == null || !IsJewelAffix(__1.affixId)) return;
            __result = Math.Clamp(__result, MinimumTier(__1.affixId) - 1, 6);
        }
    }
}
