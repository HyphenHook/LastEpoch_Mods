using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LastEpoch_Hud.Scripts.Core.Heorot;
using UnityEngine;
using StatList = Il2CppSystem.Collections.Generic.List<Il2Cpp.Stats.Stat>;

namespace LastEpoch_Hud.Scripts.Mods.Character.Heorot;

/// <summary>Target-dependent damage and source-dependent Chill. Flat/chance/resistance stats belong to HeorotBuffs.</summary>
internal static class HeorotCombat
{
    private const int ColdIndex = (int)DamageType.COLD;
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
        internal float BuiltCold;
        internal Actor Owner;
        internal readonly Dictionary<float, float> ColdByIncrease = new();
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
        if (recipes.Count == 0 || HeorotBuffs.IsActive(Refs_Manager.player_actor, HeorotBuffKind.Power)) return;
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
                || __result.damage == null || __result.damage.Length <= ColdIndex) return;
            var owner = __2.TryCast<BaseStats>()?.actor;
            if (!HeorotBuffs.IsActive(owner, HeorotBuffKind.Power)) return;
            var baseCopy = new DamageStatsHolder.BaseDamageStats(__0);
            if (__0.damage != null) baseCopy.damage = new Il2CppStructArray<float>(__0.damage.ToArray());
            Remember(__result, new Recipe
            {
                Base = baseCopy, Tags = __1, Stats = __2,
                Modifiers = Snapshot(__3 ?? __2.stats), More = __4,
                Penetration = __5, PenetrationType = __6, BuiltCold = __result.damage[ColdIndex], Owner = owner
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
            if (rebuilding || __0 == null || !HeorotBuffs.IsActive(__3, HeorotBuffKind.Power)
                || __instance.actor.IsNullOrDestroyed()
                || __instance.actor.ailmentReceiver.IsNullOrDestroyed()) return;
            float increased = HeorotRules.PowerIncreasedCold(true,
                __instance.actor.ailmentReceiver.getStacks((AilmentID)HeorotRules.ChillId));
            if (increased <= 0) return;
            var recipe = Find(__0);
            if (recipe == null || recipe.Stats.IsNullOrDestroyed() || recipe.Owner != __3) return;
            rebuilding = true;
            try
            {
                if (!recipe.ColdByIncrease.TryGetValue(increased, out float cold))
                {
                    var modifiers = new StatList();
                    foreach (var stat in recipe.Modifiers) modifiers.Add(stat);
                    modifiers.Add(Stats.IncreasedStat(SP.Damage, AT.Cold, increased, 0, 0));
                    var rebuilt = DamageStats.buildDamageStats(recipe.Base, recipe.Tags, recipe.Stats,
                        modifiers, recipe.More, recipe.Penetration, recipe.PenetrationType);
                    cold = rebuilt.damage[ColdIndex];
                    recipe.ColdByIncrease[increased] = cold;
                }
                // Preserve every unrelated field/type on the application, including external
                // conditional effects. The native builder computes cold from the original inputs.
                var copy = __0.Copy();
                var values = __0.damage.ToArray();
                values[ColdIndex] = HeorotRules.RebuiltColdPreservingScale(recipe.BuiltCold,
                    values[ColdIndex], cold);
                copy.damage = new Il2CppStructArray<float>(values);
                __0 = copy;
            }
            finally { rebuilding = false; }
        }
    }

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
