using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.ModUI;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Skills;

public class Skills_Level
{
    public static bool CanRun()
    {
        return Ready() && (LevelOn() || MultiplierOn());
    }

    static bool Ready()
    {
        if (
            (Scenes.IsGameScene())
            && (!Save_Manager.instance.IsNullOrDestroyed())
            && (!Refs_Manager.player_treedata.IsNullOrDestroyed())
        )
        {
            if (
                (!Save_Manager.instance.data.IsNullOrDestroyed())
                && (!Refs_Manager.player_treedata.specialisedSkillTrees.IsNullOrDestroyed())
            )
            {
                return true;
            }
        }
        return false;
    }

    static bool LevelOn()
    {
        return Save_Manager.instance.data.Skills.Enable_SkillLevel;
    }

    static bool MultiplierOn()
    {
        return Save_Manager.instance.data.Skills.Enable_SkillLevelMultiplier;
    }

    public static byte ChosenLevel()
    {
        int level = (int)Save_Manager.instance.data.Skills.SkillLevel;
        if (level < 0)
        {
            level = 0;
        }
        if (level > byte.MaxValue)
        {
            level = byte.MaxValue;
        }
        return (byte)level;
    }

    static int Multiplier()
    {
        return PointMultiplier.Clamp(Save_Manager.instance.data.Skills.SkillLevelMultiplier);
    }

    static bool writing;
    static bool nativeAdditionalRefresh;
    static readonly Dictionary<string, byte> real_additional = new Dictionary<string, byte>();
    static SkillsPanelManager activeSkillsPanel;
    static SkillTree activeSkillTree;
    static Transform activeSkillTreeTransform;
    static bool refreshingTreeUi;
    static readonly Dictionary<string, int> before_effective_cap = new Dictionary<string, int>();

    public static void Sync()
    {
        Reapply();
        RefreshOpenTree();
    }

    public static void ApplyAll()
    {
        if (!LevelOn() && !MultiplierOn())
        {
            return;
        }
        Sync();
    }

    static void Reapply()
    {
        if (!Ready())
        {
            return;
        }
        try
        {
            writing = true;
            if (LevelOn() || MultiplierOn())
            {
                ApplyLevels();
            }
            else
            {
                RestoreLevels();
            }
            if (MultiplierOn())
            {
                ApplyPointBonus();
            }
            else
            {
                RestorePoints();
            }
            writing = false;
        }
        catch
        {
            writing = false;
        }
    }

    static void ApplyLevels()
    {
        foreach (
            LocalTreeData.SkillTreeData data in Refs_Manager.player_treedata.specialisedSkillTrees
        )
        {
            if (data == null)
            {
                continue;
            }
            data.level = BaseLevel(data);
        }
    }

    static void RestoreLevels()
    {
        foreach (
            LocalTreeData.SkillTreeData data in Refs_Manager.player_treedata.specialisedSkillTrees
        )
        {
            if (data == null)
            {
                continue;
            }
            data.level = SpecialisedAbilityManager.getAbilityLevel(data.abilityXp);
        }
    }

    static int RawLevel(LocalTreeData.SkillTreeData data)
    {
        int level = SpecialisedAbilityManager.getAbilityLevel(data.abilityXp);
        if (level < 0)
        {
            level = 0;
        }
        if (level > byte.MaxValue)
        {
            level = byte.MaxValue;
        }
        return level;
    }

    static byte BaseLevel(LocalTreeData.SkillTreeData data)
    {
        return LevelOn() ? ChosenLevel() : (byte)RawLevel(data);
    }

    static byte EffectiveAdditional(LocalTreeData.SkillTreeData data, int rawAdditional)
    {
        long baseLevel = BaseLevel(data);
        long total = baseLevel + rawAdditional;
        if (MultiplierOn())
        {
            total *= Multiplier();
        }

        // Keep the real/base skill level in data.level. The multiplier only
        // expands the point-cap contribution stored in additionalMaxPointsFromStats.
        // This prevents native XP/max-level/refund code from ever seeing a fake
        // multiplied skill level while preserving the same effective total cap.
        long additional = total - baseLevel;
        if (additional < 0)
        {
            additional = 0;
        }
        if (additional > byte.MaxValue)
        {
            additional = byte.MaxValue;
        }
        return (byte)additional;
    }

    static string Key(LocalTreeData.SkillTreeData data)
    {
        if (data == null || data.ability.IsNullOrDestroyed())
        {
            return null;
        }
        return data.slot + ":" + data.ability.abilityName;
    }

    static void ApplyPointBonus()
    {
        foreach (
            LocalTreeData.SkillTreeData data in Refs_Manager.player_treedata.specialisedSkillTrees
        )
        {
            string key = Key(data);
            if (key == null)
            {
                continue;
            }
            if (!real_additional.ContainsKey(key))
            {
                real_additional[key] = data.additionalMaxPointsFromStats;
            }
            data.additionalMaxPointsFromStats = EffectiveAdditional(data, real_additional[key]);
        }
    }

    static void RestorePoints()
    {
        foreach (
            LocalTreeData.SkillTreeData data in Refs_Manager.player_treedata.specialisedSkillTrees
        )
        {
            string key = Key(data);
            if (key == null || !real_additional.ContainsKey(key))
            {
                continue;
            }
            data.additionalMaxPointsFromStats = real_additional[key];
        }
        real_additional.Clear();
    }

    static void OnAdditionalPointsUpdating()
    {
        if (writing || !Ready())
        {
            return;
        }
        try
        {
            nativeAdditionalRefresh = true;
            before_effective_cap.Clear();
            writing = true;

            // Snapshot the currently exposed effective cap. The native stat refresh
            // runs periodically even when gear did not change, so UI rebuilding must
            // be gated on an actual cap change.
            foreach (
                LocalTreeData.SkillTreeData data in Refs_Manager
                    .player_treedata
                    .specialisedSkillTrees
            )
            {
                if (data == null)
                {
                    continue;
                }
                string key = Key(data);
                if (key == null)
                {
                    continue;
                }
                before_effective_cap[key] = data.level + data.additionalMaxPointsFromStats;
            }

            // Give LE the raw/native state only long enough for it to recalculate
            // the new +skills contribution from the changed gear.
            foreach (
                LocalTreeData.SkillTreeData data in Refs_Manager
                    .player_treedata
                    .specialisedSkillTrees
            )
            {
                if (data == null)
                {
                    continue;
                }
                string key = Key(data);
                if (key == null)
                {
                    continue;
                }

                if (LevelOn() || MultiplierOn())
                {
                    data.level = (byte)RawLevel(data);
                }

                if (MultiplierOn() && real_additional.ContainsKey(key))
                {
                    data.additionalMaxPointsFromStats = real_additional[key];
                }
            }

            writing = false;
        }
        catch
        {
            writing = false;
            nativeAdditionalRefresh = false;
        }
    }

    static void OnAdditionalPointsUpdated()
    {
        if (writing || !Ready())
        {
            nativeAdditionalRefresh = false;
            return;
        }

        bool capChanged = false;
        try
        {
            writing = true;

            // The native calculation has completed. Cache the NEW real +skills
            // contribution, then make the stored tree state effective/multiplied.
            if (MultiplierOn())
            {
                real_additional.Clear();
                foreach (
                    LocalTreeData.SkillTreeData data in Refs_Manager
                        .player_treedata
                        .specialisedSkillTrees
                )
                {
                    string key = Key(data);
                    if (key == null)
                    {
                        continue;
                    }
                    real_additional[key] = data.additionalMaxPointsFromStats;
                }
            }
            else
            {
                real_additional.Clear();
            }

            if (LevelOn() || MultiplierOn())
            {
                ApplyLevels();
            }
            if (MultiplierOn())
            {
                ApplyPointBonus();
            }

            foreach (
                LocalTreeData.SkillTreeData data in Refs_Manager
                    .player_treedata
                    .specialisedSkillTrees
            )
            {
                if (data == null)
                {
                    continue;
                }
                string key = Key(data);
                if (key == null)
                {
                    continue;
                }

                int after = data.level + data.additionalMaxPointsFromStats;
                if (!before_effective_cap.TryGetValue(key, out int before) || before != after)
                {
                    capChanged = true;
                    break;
                }
            }

            writing = false;
        }
        catch
        {
            writing = false;
        }
        finally
        {
            nativeAdditionalRefresh = false;
            before_effective_cap.Clear();

            if (capChanged)
            {
                RefreshOpenTree();
            }
        }
    }

    static void RefreshOpenTree()
    {
        if (refreshingTreeUi)
        {
            return;
        }

        try
        {
            // Re-run the same LE path used when the player switches away from a
            // specialization and opens it again. Simple updateVisuals()/updateText()
            // calls do not rebuild the active tree's cached allocation state.
            if (
                !activeSkillsPanel.IsNullOrDestroyed()
                && !activeSkillTree.IsNullOrDestroyed()
                && !activeSkillTreeTransform.IsNullOrDestroyed()
            )
            {
                refreshingTreeUi = true;
                activeSkillsPanel.OnOpenSkillTree(activeSkillTree, activeSkillTreeTransform);
                activeSkillsPanel.updateVisuals(false);
                refreshingTreeUi = false;
                return;
            }

            // Fallback when no active specialization has been cached yet.
            foreach (SkillsPanelManager panel in Object.FindObjectsOfType<SkillsPanelManager>())
            {
                if (!panel.IsNullOrDestroyed())
                {
                    panel.updateVisuals(false);
                }
            }
        }
        catch (System.Exception ex)
        {
            refreshingTreeUi = false;
            Main.logger_instance?.Warning("Skill tree UI refresh failed: " + ex.Message);
        }
    }

    [HarmonyPatch(typeof(SkillsPanelManager), "OnOpenSkillTree")]
    public class SkillsPanelManager_OnOpenSkillTree
    {
        [HarmonyPostfix]
        static void Postfix(SkillsPanelManager __instance, SkillTree __0, Transform __1)
        {
            try
            {
                if (__0.IsNullOrDestroyed())
                {
                    return;
                }

                activeSkillsPanel = __instance;
                activeSkillTree = __0;
                activeSkillTreeTransform = __1;

                // RefreshOpenTree deliberately re-enters OnOpenSkillTree to force LE
                // to rebuild the active specialization. Do not recursively Sync().
                if (refreshingTreeUi)
                {
                    return;
                }

                Sync();
                if (!__instance.IsNullOrDestroyed())
                {
                    __instance.updateVisuals(false);
                }
            }
            catch
            {
                Main.logger_instance?.Msg("SkillsPanelManager.OnOpenSkillTree() ERROR");
            }
        }
    }

    [HarmonyPatch(typeof(LocalTreeData), "getAbilityLevel")]
    public class LocalTreeData_getAbilityLevel
    {
        [HarmonyPostfix]
        static void Postfix(LocalTreeData __instance, Ability __0, ref byte __result)
        {
            if (
                writing
                || !CanRun()
                || __0.IsNullOrDestroyed()
                || __instance.specialisedSkillTrees.IsNullOrDestroyed()
            )
            {
                return;
            }

            foreach (LocalTreeData.SkillTreeData data in __instance.specialisedSkillTrees)
            {
                if (data == null || data.ability.IsNullOrDestroyed())
                {
                    continue;
                }
                if (data.ability.abilityName != __0.abilityName)
                {
                    continue;
                }

                // getAbilityLevel is used by LE for native skill-XP state,
                // max-level checks and manual point refunds. It must always see
                // the unmultiplied/base level. The multiplier is represented only
                // as synthetic additional point capacity.
                byte baseLevel = BaseLevel(data);

                if (nativeAdditionalRefresh)
                {
                    // Gear-stat recalculation specifically needs the true XP level,
                    // even when the separate fixed Skill Level option is enabled.
                    byte raw = (byte)RawLevel(data);
                    writing = true;
                    data.level = raw;
                    writing = false;
                    __result = raw;
                    return;
                }

                __result = baseLevel;
                return;
            }
        }
    }

    [HarmonyPatch(typeof(LocalTreeData), nameof(LocalTreeData.levelUpAbility))]
    public class LocalTreeData_levelUpAbility
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!CanRun())
            {
                return;
            }
            Reapply();
        }
    }

    [HarmonyPatch(typeof(LocalTreeData), nameof(LocalTreeData.ApplyAbilityXp))]
    public class LocalTreeData_ApplyAbilityXp
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!CanRun())
            {
                return;
            }
            Reapply();
        }
    }

    [HarmonyPatch(typeof(LocalTreeData), nameof(LocalTreeData.LevelSkillsToMinLevel))]
    public class LocalTreeData_LevelSkillsToMinLevel
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!CanRun())
            {
                return;
            }
            Reapply();
        }
    }

    [HarmonyPatch(typeof(LocalTreeData), "respecNodesFromSkillIfOverInvested")]
    public class LocalTreeData_respecNodesFromSkillIfOverInvested
    {
        public struct RespecState
        {
            public bool active;
            public byte rawLevel;
            public byte rawAdditional;
            public string key;
        }

        [HarmonyPrefix]
        static void Prefix(LocalTreeData.SkillTreeData __0, out RespecState __state)
        {
            __state = default;
            if (!Ready() || !MultiplierOn() || __0 == null)
            {
                return;
            }

            string key = Key(__0);
            if (key == null)
            {
                return;
            }

            // Capture the exact raw state for this invocation. Native overflow
            // removal can trigger nested stat recalculations, so this must not
            // live in a shared dictionary that another refresh can clear.
            __state.active = true;
            __state.key = key;
            __state.rawLevel = (byte)RawLevel(__0);
            __state.rawAdditional = __0.additionalMaxPointsFromStats;

            real_additional[key] = __state.rawAdditional;

            __0.level = BaseLevel(__0);
            __0.additionalMaxPointsFromStats = EffectiveAdditional(__0, __state.rawAdditional);
        }

        [HarmonyPostfix]
        static void Postfix(LocalTreeData.SkillTreeData __0, RespecState __state)
        {
            if (!__state.active || __0 == null)
            {
                return;
            }

            // Always restore the raw representation that belonged to this exact
            // native respec call. The effective cap is carried only by the
            // temporary synthetic additional-points value above.
            __0.level = __state.rawLevel;
            __0.additionalMaxPointsFromStats = __state.rawAdditional;

            if (__state.key != null)
            {
                real_additional[__state.key] = __state.rawAdditional;
            }
        }
    }

    [HarmonyPatch(
        typeof(LocalTreeData),
        nameof(LocalTreeData.setAdditionalMaxPointsFromStatsOnServerOrInSingleplayer)
    )]
    public class LocalTreeData_setAdditionalMaxPointsFromStats
    {
        [HarmonyPrefix]
        static void Prefix()
        {
            OnAdditionalPointsUpdating();
        }

        [HarmonyPostfix]
        static void Postfix()
        {
            OnAdditionalPointsUpdated();
        }
    }

    [HarmonyPatch(
        typeof(LocalTreeData),
        nameof(LocalTreeData.receiveAdditionalMaxPointsFromStatsOnClient)
    )]
    public class LocalTreeData_receiveAdditionalMaxPointsFromStatsOnClient
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            OnAdditionalPointsUpdated();
        }
    }
}
