using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LastEpoch_Hud.Scripts.Core.Heorot;
using LastEpoch_Hud.Scripts.Mods.Character.Volcanic;
using UnityEngine;
using StatList = Il2CppSystem.Collections.Generic.List<Il2Cpp.Stats.Stat>;

namespace LastEpoch_Hud.Scripts.Mods.Character.Heorot;

/// <summary>Target-dependent damage and source-dependent Chill. Flat/chance/resistance stats belong to HeorotBuffs.</summary>
internal static class HeorotCombat
{
    private const int ColdIndex = (int)DamageType.COLD;
    private const int FireIndex = (int)DamageType.FIRE;
    private const int NecroticIndex = (int)DamageType.NECROTIC;
    [ThreadStatic] private static bool rebuilding;

    // Cached skill/ailment damage has already consumed the increased-damage pool. Keep its
    // build inputs so the target-specific stat can enter that pool, before any more modifiers,
    // conversions of damage taken, resistance, ward, or health damage. Never infer the pool by
    // dividing final damage: that fails with zero/clamped damage and added damage conversion.
    private sealed class Recipe
    {
        internal DamageStatsHolder.BaseDamageStats Base;
        internal AT Tags;
        internal Stats Stats;
        internal StatList Modifiers;
        internal float More, Penetration;
        internal DamageType PenetrationType;
        internal float[] Built;
        internal Actor Owner;
        internal readonly Dictionary<(int, float), float> RebuiltByIncrease = new();
    }

    private sealed class Entry
    {
        internal IntPtr WeakHandle;
        internal Recipe Recipe;
    }

    // Weak native handles (not managed wrapper weak references) survive wrapper recreation
    // without rooting every projectile's DamageStats for the lifetime of a zone.
    private static readonly Dictionary<IntPtr, Entry> recipes = new();
    private static readonly object recipeLock = new();
    private static int buildsUntilSweep = 256;

    // The main owner calls this after HeorotBuffs.Tick. No native query/allocation when empty.
    internal static void Tick()
    {
        if (recipes.Count == 0 || HeorotBuffs.IsActive(Refs_Manager.player_actor, HeorotBuffKind.Power)
            || VolcanicBuffs.IsActive(Refs_Manager.player_actor, VolcanicBuffKind.Wrath)) return;
        lock (recipeLock)
        {
            foreach (var entry in recipes.Values) IL2CPP.il2cpp_gchandle_free(entry.WeakHandle);
            recipes.Clear();
            buildsUntilSweep = 256;
        }
    }

    private static void Remember(DamageStats damage, Recipe recipe)
    {
        if (damage == null || recipe == null) return;
        lock (recipeLock)
        {
            if (--buildsUntilSweep <= 0)
            {
                var dead = new List<IntPtr>();
                foreach (var pair in recipes)
                    if (IL2CPP.il2cpp_gchandle_get_target(pair.Value.WeakHandle) == IntPtr.Zero)
                        dead.Add(pair.Key);
                foreach (var key in dead)
                {
                    IL2CPP.il2cpp_gchandle_free(recipes[key].WeakHandle);
                    recipes.Remove(key);
                }
                buildsUntilSweep = 256;
            }
            if (recipes.TryGetValue(damage.Pointer, out var previous))
                IL2CPP.il2cpp_gchandle_free(previous.WeakHandle);
            recipes[damage.Pointer] = new Entry
            {
                WeakHandle = IL2CPP.il2cpp_gchandle_new_weakref(damage.Pointer, false),
                Recipe = recipe
            };
        }
    }

    private static Recipe Find(DamageStats damage)
    {
        if (damage == null) return null;
        lock (recipeLock)
            return recipes.TryGetValue(damage.Pointer, out var entry)
                && IL2CPP.il2cpp_gchandle_get_target(entry.WeakHandle) == damage.Pointer
                    ? entry.Recipe : null;
    }

    private static StatList Snapshot(StatList source)
    {
        var result = new StatList();
        if (source != null)
            foreach (var stat in source)
                if (stat != null) result.Add(new Stats.Stat(stat));
        return result;
    }

    [HarmonyPatch(typeof(DamageStats), nameof(DamageStats.buildDamageStats))]
    private static class CaptureDamageBuild
    {
        private static void Postfix(DamageStatsHolder.BaseDamageStats __0, AT __1, Stats __2,
            StatList __3, float __4, float __5, DamageType __6, DamageStats __result)
        {
            if (rebuilding || __0 == null || __2.IsNullOrDestroyed() || __result == null
                || __result.damage == null || __result.damage.Length <= Math.Max(ColdIndex, FireIndex)) return;
            var owner = __2.TryCast<BaseStats>()?.actor;
            if (!HeorotBuffs.IsActive(owner, HeorotBuffKind.Power) && !VolcanicBuffs.IsActive(owner, VolcanicBuffKind.Wrath)) return;
            var baseCopy = new DamageStatsHolder.BaseDamageStats(__0);
            if (__0.damage != null) baseCopy.damage = new Il2CppStructArray<float>(__0.damage.ToArray());
            Remember(__result, new Recipe
            {
                Base = baseCopy, Tags = __1, Stats = __2,
                Modifiers = Snapshot(__3 ?? __2.stats), More = __4,
                Penetration = __5, PenetrationType = __6, Built = __result.damage.ToArray(), Owner = owner
            });
        }
    }

    [HarmonyPatch(typeof(DamageStats), nameof(DamageStats.Copy))]
    private static class CopyDamageBuild
    {
        private static void Postfix(DamageStats __instance, DamageStats __result)
        {
            if (!rebuilding) Remember(__result, Find(__instance));
        }
    }

    [HarmonyPatch(typeof(ProtectionClass), nameof(ProtectionClass.ApplyDamage))]
    private static class PowerDamage
    {
        // Run before effects that intentionally modify the finished outgoing damage (e.g. jewels).
        [HarmonyPriority(Priority.First)]
        private static void Prefix(ProtectionClass __instance, ref DamageStats __0, Actor __3)
        {
            if (rebuilding || __0 == null || __instance.actor.IsNullOrDestroyed()
                || __instance.actor.ailmentReceiver.IsNullOrDestroyed()) return;
            bool power = HeorotBuffs.IsActive(__3, HeorotBuffKind.Power);
            bool wrath = VolcanicBuffs.IsActive(__3, VolcanicBuffKind.Wrath);
            if (power || wrath)
            {
                var receiver = __instance.actor.ailmentReceiver;
                var rules = new List<(int Index, AT Tag, float Increased)>(2);
                if (power)
                {
                    float increased = HeorotRules.PowerIncreasedCold(true, receiver.getStacks((AilmentID)HeorotRules.ChillId),
                        HeorotBuffs.EffectOf(__3, HeorotBuffKind.Power));
                    if (increased > 0) rules.Add((ColdIndex, AT.Cold, increased));
                }
                if (wrath)
                {
                    float increased = VolcanicValues.WrathIncreasedFire(receiver.getStacks(AilmentID.Ignite),
                        VolcanicBuffs.EffectOf(__3, VolcanicBuffKind.Wrath));
                    if (increased > 0) rules.Add((FireIndex, AT.Fire, increased));
                }
                if (rules.Count > 0) __0 = Rebuilt(__0, rules, __3);
            }
            Leech(__0, __3, __instance.actor);
        }
    }

    // The target-dependent increased damage of Heorot's Power (Cold, per Chill) and Bhuldar's Wrath (Fire, per Ignite):
    // each rebuilds only its own damage type from the original inputs, so the two never feed each other.
    private static DamageStats Rebuilt(DamageStats damage, List<(int Index, AT Tag, float Increased)> rules, Actor owner)
    {
        var recipe = Find(damage);
        if (recipe == null || recipe.Stats.IsNullOrDestroyed() || recipe.Owner != owner) return damage;
        rebuilding = true;
        try
        {
            var values = damage.damage.ToArray();
            foreach (var (index, tag, increased) in rules)
            {
                if (index >= values.Length || index >= recipe.Built.Length) continue;
                if (!recipe.RebuiltByIncrease.TryGetValue((index, increased), out float rebuiltValue))
                {
                    var modifiers = new StatList();
                    foreach (var stat in recipe.Modifiers) modifiers.Add(stat);
                    modifiers.Add(Stats.IncreasedStat(SP.Damage, tag, increased, 0, 0));
                    var rebuilt = DamageStats.buildDamageStats(recipe.Base, recipe.Tags, recipe.Stats,
                        modifiers, recipe.More, recipe.Penetration, recipe.PenetrationType);
                    rebuiltValue = rebuilt.damage[index];
                    recipe.RebuiltByIncrease[(index, increased)] = rebuiltValue;
                }
                // Preserve every unrelated field/type on the application, including external
                // conditional effects. The native builder computes the type from the original inputs.
                values[index] = HeorotRules.RebuiltColdPreservingScale(recipe.Built[index], values[index], rebuiltValue);
            }
            var copy = damage.Copy();
            copy.damage = new Il2CppStructArray<float>(values);
            return copy;
        }
        finally { rebuilding = false; }
    }

    // Logi's Hunger: a share of the Fire and Necrotic damage the wearer deals comes back as Health.
    private static void Leech(DamageStats damage, Actor attacker, Actor target)
    {
        if (damage?.damage == null || attacker.IsNullOrDestroyed() || attacker == target
            || damage.damage.Length <= NecroticIndex || !VolcanicBuffs.IsActive(attacker, VolcanicBuffKind.Hunger)) return;
        float health = VolcanicValues.LeechedHealth(damage.damage[FireIndex], damage.damage[NecroticIndex]);
        if (health > 0 && float.IsFinite(health) && !attacker.health.IsNullOrDestroyed()) attacker.health.heal(health);
    }

    // Herkir's Vessel: whoever hits the wearer may be set alight. The game has no Ignite counterpart of its Chill retaliation.
    [HarmonyPatch(typeof(AilmentReceiver), nameof(AilmentReceiver.whenHit))]
    private static class IgniteAttackers
    {
        private static void Postfix(AilmentReceiver __instance, Actor __1)
        {
            var wearer = __instance.actor;
            if (wearer.IsNullOrDestroyed() || __1.IsNullOrDestroyed() || __1 == wearer || __1.ailmentReceiver.IsNullOrDestroyed()
                || !VolcanicBuffs.IsActive(wearer, VolcanicBuffKind.Vessel)) return;
            float chance = VolcanicValues.IgniteAttackersChance * (1f + VolcanicBuffs.EffectOf(wearer, VolcanicBuffKind.Vessel));
            int stacks = VolcanicValues.IgniteStacks(chance, ignite.NextDouble());
            if (stacks <= 0) return;
            // The burn deals Fire damage in proportion to the wearer's UNCAPPED Fire Resistance (every point of it, above the
            // game's cap too), so it needs a damage record of its own.
            float resistance = UncappedFireResistance(wearer);
            float damage = VolcanicValues.IgniteDamage(resistance);
            try
            {
                var definition = AilmentList.getAilment(AilmentID.Ignite);
                if (damage > 0 && !definition.IsNullOrDestroyed())
                {
                    var record = new DamageStats();
                    var amounts = new float[7];
                    amounts[FireIndex] = damage;
                    record.damage = new Il2CppStructArray<float>(amounts);
                    record.baseDamageTypeTags = AT.Fire;
                    __1.ailmentReceiver.ApplyAilmentWithDamageStats(definition, wearer, null, null, null, record,
                        wearer.GetAlignment(), UseType.AilmentProc, stacks, 0f, 0f, 0f, false, 0f);
                    if (igniteLogged++ < 5)
                        Main.logger_instance?.Msg("Herkir’s Vessel: ignited " + __1.gameObject.name + " with " + stacks + " stack(s) of "
                            + damage + " Fire damage (Fire Resistance " + resistance + ").");
                    return;
                }
            }
            catch (Exception ex)
            {
                if (igniteLogged++ < 5) Main.logger_instance?.Warning("Herkir’s Vessel: Ignite with damage failed (" + ex.Message + "); using a plain Ignite.");
            }
            __1.ailmentReceiver.ApplyStacksOfAilment(stacks, AilmentID.Ignite, wearer, 0f, 0f, false);
        }
    }

    // The game's own uncapped total (5.32 = 532%), which includes every source. Summing the Fire Resistance stat alone misses some.
    internal static float UncappedFireResistance(Actor actor)
    {
        try
        {
            if (!actor.IsNullOrDestroyed() && !actor.protection.IsNullOrDestroyed())
            {
                float value = actor.protection.uncappedFireResistance;
                if (float.IsFinite(value)) return value;
            }
        }
        catch (Exception ex) { ErrorLog.Report(ex, "Uncapped Fire Resistance"); }
        return actor.IsNullOrDestroyed() || actor.stats.IsNullOrDestroyed() ? 0f : actor.stats.GetTotalAdded(SP.FireResistance, AT.None, 0, 0, false);
    }

    private static int igniteLogged;

    private static readonly System.Random ignite = new();

    private static Ailment canonicalChill, ordinaryChill, defenceChill;

    private static Ailment ChillForSource(Ailment incoming, Actor source, AilmentReceiver receiver)
    {
        if (incoming.IsNullOrDestroyed() || (byte)incoming.id != HeorotRules.ChillId) return incoming;
        // Resolve by ID even for a spread from an existing private definition. An attacker
        // without Defence must never inherit the raised limit from that existing instance.
        var canonical = AilmentList.getAilment((AilmentID)HeorotRules.ChillId);
        if (canonical.IsNullOrDestroyed()) return incoming;
        bool defence = HeorotBuffs.IsActive(source, HeorotBuffKind.Defence);
        if (!defence && defenceChill.IsNullOrDestroyed()) return canonical;
        if (defenceChill.IsNullOrDestroyed() || canonicalChill != canonical
            || defenceChill.maxInstances != HeorotRules.ChillLimit(canonical.maxInstances, true)
            || defenceChill.maxStacksThatApplyBuffs != HeorotRules.ChillLimit(canonical.maxStacksThatApplyBuffs, true))
        {
            // Only the private clone changes. Native application, replacement, timers, immunity,
            // stat effects, cleansing, UI stack sync and expiration remain in AilmentReceiver.
            canonicalChill = canonical;
            ordinaryChill = UnityEngine.Object.Instantiate(canonical);
            ordinaryChill.hideFlags = HideFlags.HideAndDontSave;
            ordinaryChill.name = "Heorot_OrdinarySourceChill";
            // The effect cap must agree for every instance on a mixed-source target.
            // Otherwise native min(stacks, cap) deltas on removal leave residual slow.
            // This clone still has the ORDINARY application limit (normally three).
            ordinaryChill.maxStacksThatApplyBuffs = HeorotRules.ChillLimit(canonical.maxStacksThatApplyBuffs, true);
            defenceChill = UnityEngine.Object.Instantiate(canonical);
            defenceChill.hideFlags = HideFlags.HideAndDontSave;
            defenceChill.name = "Heorot_SourceScopedChill";
            defenceChill.maxInstances = HeorotRules.ChillLimit(canonical.maxInstances, true);
            defenceChill.maxStacksThatApplyBuffs = HeorotRules.ChillLimit(canonical.maxStacksThatApplyBuffs, true);
        }
        if (defence && receiver.ailments != null)
            foreach (var active in receiver.ailments)
                if (active != null && !active.ailment.IsNullOrDestroyed()
                    && (byte)active.ailment.id == HeorotRules.ChillId
                    && active.ailment.Pointer == canonical.Pointer)
                    active.ailment = ordinaryChill;
        return defence ? defenceChill : ordinaryChill;
    }

    // The common native terminal path, including on-hit, retaliation, duration applications,
    // multiple stacks and spreading. Do not patch the global Ailment.getMaxStacks getter.
    [HarmonyPatch(typeof(AilmentReceiver), nameof(AilmentReceiver.ApplyAilmentWithDamageStats))]
    private static class SourceScopedChill
    {
        private static void Prefix(AilmentReceiver __instance, ref Ailment __0, Actor __1) =>
            __0 = ChillForSource(__0, __1, __instance);
    }
}
