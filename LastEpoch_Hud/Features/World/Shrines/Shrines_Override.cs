using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Shrines;

public class Shrines_Override
{
    /*  0 : Haste Shrine
        1 : Shard Shrine
        2 : Reflect Shrine
        3 : Stun Shrine
        4 : Gold Shrine
        5 : Unique Shrine
        6 : Idol Shrine
        7 : Crit Shrine
        8 : Manatee Shrine
        9 : Ice Beetle Shrine
        10 : Experience Shrine
        11 : Many Ice Beetles Shrine
        12 : Bee Shrine
        13 : Acid Flask Shrine
        14 : Dungeon Floor Exit Shrine
        15 : Stop Hammer Shrine
        16 : Maelstrom Shrine
        17 : Lightning Blast Shrine
        18 : Rip Blood Shrine
        19 : Primal Lightning Shrine
        20 : Fireball Shrine
        21 : Smite Shrine
        22 : Chaos Bolts Shrine
        23 : Shuriken Shrine
        24 : Timeslow Aura Shrine
        25 : Freezing Nova Shrine
        26 : Nature Beam Shrine
        27 : Void Squirrels Shrine
        28 : Rainbow Power Shrine
        29 : Meteor Storm Shrine
        30 : Necrotic Explosions Shrine
        31 : Thorns Shrine
        32 : Phoenix Shrine
        33 : Loot Lizard Shrine - Low Level
        34 : Loot Lizard Shrine - Higher Level
        35 : Upheaval Shrine
        36 : Flame Reave Shrine
        37 : Harvest Shrine
        38 : Warpath Shrine
        39 : Shadow Cascade Shrine
        40 : Distant Damage Reduction Shrine
    */

    public static bool CanRun()
    {
        if (!Save_Manager.instance.IsNullOrDestroyed())
        {
            if (!Save_Manager.instance.data.IsNullOrDestroyed())
            {
                return Save_Manager.instance.data.modsNotInHud.Shrines_Override;
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

    [HarmonyPatch(
        typeof(ShrinesManager),
        "PlaceNewShrine",
        new System.Type[] { typeof(int), typeof(Vector3) }
    )]
    public class ShrinesManager_PlaceNewShrine
    {
        [HarmonyPrefix]
        static void Prefix(ref int __0)
        {
            if (CanRun())
            {
                int id = Save_Manager.instance.data.modsNotInHud.Shrines_Override_id;
                if (
                    !ShrineList.get().IsNullOrDestroyed()
                    && id >= 0
                    && id < ShrineList.get().entries.Count
                )
                {
                    __0 = id;
                }
            }
        }
    }
}
