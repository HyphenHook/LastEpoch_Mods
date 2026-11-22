using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.ModUI;

namespace LastEpoch_Hud.Scripts.Mods.Skills;

public class Passives_Points
{
    public static bool CanRun()
    {
        if (
            (Scenes.IsGameScene())
            && (!Save_Manager.instance.IsNullOrDestroyed())
            && (!Refs_Manager.player_treedata.IsNullOrDestroyed())
        )
        {
            if (!Save_Manager.instance.data.IsNullOrDestroyed())
            {
                return Save_Manager.instance.data.Skills.Enable_PassivePoints
                    || Save_Manager.instance.data.Skills.Enable_PassivePointMultiplier;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }

    static int RealPoints()
    {
        return Refs_Manager.player_treedata.calculatePassivePointsEarnt();
    }

    static int Target()
    {
        int points = Save_Manager.instance.data.Skills.Enable_PassivePoints
            ? (int)Save_Manager.instance.data.Skills.PassivePoints
            : RealPoints();
        if (points < 0)
        {
            points = 0;
        }
        if (Save_Manager.instance.data.Skills.Enable_PassivePointMultiplier)
        {
            points *= PointMultiplier.Clamp(
                Save_Manager.instance.data.Skills.PassivePointMultiplier
            );
        }
        if (points > ushort.MaxValue)
        {
            points = ushort.MaxValue;
        }
        return points;
    }

    public static void Sync()
    {
        if (CanRun())
        {
            Apply();
        }
        else
        {
            Restore();
        }
    }

    public static void Apply()
    {
        try
        {
            if (!CanRun())
            {
                return;
            }
            if (
                Refs_Manager.player_treedata.IsNullOrDestroyed()
                || Refs_Manager.player_treedata.passiveTree == null
            )
            {
                return;
            }
            Refs_Manager.player_treedata.passiveTree.pointsEarnt = (ushort)Target();
        }
        catch { }
    }

    public static void Restore()
    {
        try
        {
            if (
                Refs_Manager.player_treedata.IsNullOrDestroyed()
                || Refs_Manager.player_treedata.passiveTree == null
            )
            {
                return;
            }
            Refs_Manager.player_treedata.passiveTree.pointsEarnt =
                Refs_Manager.player_treedata.calculatePassivePointsEarnt();
        }
        catch { }
    }

    [HarmonyPatch(typeof(PassivePanelManager), "onTreeOpened")]
    public class PassivePanelManager_onTreeOpened
    {
        [HarmonyPrefix]
        static void Prefix(PassivePanelManager __instance, CharacterClass __0, byte __1)
        {
            if (CanRun())
            {
                Apply();
            }
        }
    }

    [HarmonyPatch(
        typeof(LocalTreeData),
        nameof(LocalTreeData.receiveUpdatePassivePointsEarntCommand)
    )]
    public class LocalTreeData_receiveUpdatePassivePointsEarntCommand
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!CanRun())
            {
                return;
            }
            Apply();
        }
    }

    [HarmonyPatch(
        typeof(LocalTreeData),
        nameof(LocalTreeData.setCharacterLevel),
        new System.Type[] { typeof(int) }
    )]
    public class LocalTreeData_setCharacterLevelInt
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!CanRun())
            {
                return;
            }
            Apply();
        }
    }

    [HarmonyPatch(
        typeof(LocalTreeData),
        nameof(LocalTreeData.setCharacterLevel),
        new System.Type[] { typeof(byte) }
    )]
    public class LocalTreeData_setCharacterLevelByte
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!CanRun())
            {
                return;
            }
            Apply();
        }
    }
}
