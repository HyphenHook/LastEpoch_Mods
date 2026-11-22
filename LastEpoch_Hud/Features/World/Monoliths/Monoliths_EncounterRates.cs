using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Factions;
using LastEpoch_Hud.Scripts.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Settings;

namespace LastEpoch_Hud.Scripts.Mods.Monoliths;

/// <summary>Echo Encounters: guaranteed spawns and exact counts for random encounters in echoes.</summary>
internal static class Monoliths_EncounterRates
{
    // Far above 1, so the Roll succeeds whatever the zone multiplier or Weaver modifiers.
    private const float Guaranteed = 1000f;

    // The game's own table values, captured once before the first change.
    private static float[] _original = null;

    // The Weaver's Favour Cache chance while raised for a placement, to put back afterwards.
    private static TheWeaver _raisedWeaver = null;
    private static float _originalFavourChance = 0f;

    // Guards the extra Favour Cache spawns against counting themselves.
    private static bool _spawningExtraFavourCaches = false;

    private static bool IsEcho(RandomEncounterTypes.EncounterPlacementData placementData) =>
        placementData?.MonolithData != null && placementData.MonolithData.HasMonolithData;

    // The slider's range; a hand-edited save can't go past it.
    private const int MaxCount = 10;

    /// <summary>The configured count (rounded, 0-10), or -1 when the setting is off.</summary>
    private static int Count(FloatSetting setting) =>
        setting.Enabled ? Math.Clamp((int)Math.Round(setting.Value), 0, MaxCount) : -1;

    private static void Apply(
        RandomEncounterData d,
        RandomEncounterTypes.EncounterPlacementData placementData,
        bool echo
    )
    {
        if (_original == null)
        {
            _original = new[]
            {
                d.baseNemesisEncounterSpawnRate,
                d.nemesisEncounterSpawnRatePerLevel,
                d.nemesisEncounterSpawnRatePerCorruption,
                d.baseRunePrisonSpawnRate,
                d.runePrisonSpawnRatePerLevel,
                d.runePrisonSpawnRatePerCorruption,
                d.baseEndgameCacheSpawnRate,
                d.endgameCacheSpawnRatePerLevel,
                d.endgameCacheSpawnRatePerCorruption,
                d.baseObserverWindowMonolithSpawnRate,
                d.observerWindowSpawnRatePerCorruption,
                d.baseRageOfMorditasMonolithSpawnRate,
                d.baseTimeBeastSpawnRate,
                d.timeBeastSpawnRatePerLevel,
                d.timeBeastSpawnRatePerCorruption,
            };
        }

        // Start from the game's values every time, then change only what's on, in echoes only.
        d.baseNemesisEncounterSpawnRate = _original[0];
        d.nemesisEncounterSpawnRatePerLevel = _original[1];
        d.nemesisEncounterSpawnRatePerCorruption = _original[2];
        d.baseRunePrisonSpawnRate = _original[3];
        d.runePrisonSpawnRatePerLevel = _original[4];
        d.runePrisonSpawnRatePerCorruption = _original[5];
        d.baseEndgameCacheSpawnRate = _original[6];
        d.endgameCacheSpawnRatePerLevel = _original[7];
        d.endgameCacheSpawnRatePerCorruption = _original[8];
        d.baseObserverWindowMonolithSpawnRate = _original[9];
        d.observerWindowSpawnRatePerCorruption = _original[10];
        d.baseRageOfMorditasMonolithSpawnRate = _original[11];
        d.baseTimeBeastSpawnRate = _original[12];
        d.timeBeastSpawnRatePerLevel = _original[13];
        d.timeBeastSpawnRatePerCorruption = _original[14];
        if (!echo)
        {
            return;
        }

        if (ModSettings.EchoEncounters.AlwaysNemesis.Value)
        {
            d.baseNemesisEncounterSpawnRate = Guaranteed;
        }
        if (ModSettings.EchoEncounters.AlwaysLostCache.Value)
        {
            d.baseEndgameCacheSpawnRate = Guaranteed;
        }
        if (ModSettings.EchoEncounters.AlwaysOmenWindow.Value)
        {
            d.baseObserverWindowMonolithSpawnRate = Guaranteed;
        }
        if (ModSettings.EchoEncounters.AlwaysBloodRitual.Value)
        {
            d.baseRageOfMorditasMonolithSpawnRate = Guaranteed;
        }
        if (ModSettings.EchoEncounters.AlwaysRiftBeast.Value)
        {
            d.baseTimeBeastSpawnRate = Guaranteed;
        }

        int prisons = Count(ModSettings.EchoEncounters.RunePrisonCount);
        if (prisons >= 0)
        {
            float zoneMultiplier = placementData?.SceneData?.Config?.SpawnRateMultiplier ?? 1f;
            d.baseRunePrisonSpawnRate = zoneMultiplier > 0f ? prisons / zoneMultiplier : prisons;
            d.runePrisonSpawnRatePerLevel = 0f;
            d.runePrisonSpawnRatePerCorruption = 0f;
        }
    }

    private static void RaiseFavourChance(RandomEncounterTypes.EncounterPlacementData placementData)
    {
        TheWeaver weaver = placementData?.WeaverModifiers;
        if (weaver == null || Count(ModSettings.EchoEncounters.FavourCacheCount) <= 0)
        {
            return;
        }
        _raisedWeaver = weaver;
        _originalFavourChance = weaver.SpawnFavorCacheChance;
        weaver.SpawnFavorCacheChance = 1f;
    }

    private static void RestoreFavourChance()
    {
        if (_raisedWeaver != null)
        {
            _raisedWeaver.SpawnFavorCacheChance = _originalFavourChance;
            _raisedWeaver = null;
        }
    }

    [HarmonyPatch(
        typeof(RandomEncounterPlacer),
        nameof(RandomEncounterPlacer.TryPlaceRandomEncounters)
    )]
    public class RandomEncounterPlacer_TryPlaceRandomEncounters
    {
        [HarmonyPrefix]
        static void Prefix(RandomEncounterTypes.EncounterPlacementData __1)
        {
            try
            {
                RandomEncounterData data = RandomEncounterData.get();
                bool echo = IsEcho(__1);
                if (!data.IsNullOrDestroyed())
                {
                    Apply(data, __1, echo);
                }
                if (echo)
                {
                    RaiseFavourChance(__1);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Report(ex, "Monoliths_EncounterRates");
            }
        }

        [HarmonyPostfix]
        static void Postfix()
        {
            try
            {
                RestoreFavourChance();
            }
            catch (Exception ex)
            {
                ErrorLog.Report(ex, "Monoliths_EncounterRates");
            }
        }
    }

    [HarmonyPatch(
        typeof(RandomRegularRunePrisonSpawner),
        nameof(RandomRegularRunePrisonSpawner.CanSpawn)
    )]
    public class RandomRegularRunePrisonSpawner_CanSpawn
    {
        [HarmonyPrefix]
        static bool Prefix(RandomEncounterTypes.EncounterPlacementData __0, ref bool __result)
        {
            if (IsEcho(__0) && Count(ModSettings.EchoEncounters.RunePrisonCount) == 0)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(RandomFavourCacheSpawner), nameof(RandomFavourCacheSpawner.CanSpawn))]
    public class RandomFavourCacheSpawner_CanSpawn
    {
        [HarmonyPrefix]
        static bool Prefix(RandomEncounterTypes.EncounterPlacementData __0, ref bool __result)
        {
            if (IsEcho(__0) && Count(ModSettings.EchoEncounters.FavourCacheCount) == 0)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    /// <summary>Extra Favour Caches: runs the game's own spawner again for counts above 1.</summary>
    [HarmonyPatch(typeof(RandomFavourCacheSpawner), nameof(RandomFavourCacheSpawner.ExecuteSpawn))]
    public class RandomFavourCacheSpawner_ExecuteSpawn
    {
        [HarmonyPostfix]
        static void Postfix(
            RandomFavourCacheSpawner __instance,
            RandomEncounterTypes.EncounterPlacementData __0
        )
        {
            int count = Count(ModSettings.EchoEncounters.FavourCacheCount);
            if (_spawningExtraFavourCaches || count <= 1 || !IsEcho(__0))
            {
                return;
            }
            _spawningExtraFavourCaches = true;
            try
            {
                for (int i = 1; i < count; i++)
                {
                    __instance.ExecuteSpawn(__0);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Report(ex, "Monoliths_EncounterRates");
            }
            finally
            {
                _spawningExtraFavourCaches = false;
            }
        }
    }
}
