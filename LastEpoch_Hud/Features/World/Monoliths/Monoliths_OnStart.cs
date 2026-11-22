using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Monoliths;

public class Monoliths_OnStart
{
    public static bool CanRun()
    {
        if (
            (Scenes.IsGameScene())
            && (!Save_Manager.instance.IsNullOrDestroyed())
            && (!Refs_Manager.player_actor.IsNullOrDestroyed())
        )
        {
            if (!Save_Manager.instance.data.IsNullOrDestroyed())
            {
                if (
                    (Save_Manager.instance.data.Scenes.Monoliths.Enable_MaxStability)
                    || (Save_Manager.instance.data.Scenes.Monoliths.Enable_MaxStabilityOnStart)
                    || (Save_Manager.instance.data.Scenes.Monoliths.Enable_MobsDefeatOnStart)
                    || (Save_Manager.instance.data.Scenes.Monoliths.Enable_ObjectiveReveal)
                ) //||
                //(Save_Manager.instance.data.Scenes.Monoliths.Enable_CompleteObjective))
                {
                    return true;
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
        else
        {
            return false;
        }
    }

    //MonolithProgressManager

    [HarmonyPatch(typeof(MonolithZoneManager), "initialise")]
    public class MonolithZoneManager_initialise
    {
        [HarmonyPostfix]
        static void Postfix(ref MonolithZoneManager __instance, StatefulQuestList __0)
        {
            if (Refs_Manager.monolith_zone_manager.IsNullOrDestroyed())
            {
                Refs_Manager.monolith_zone_manager = __instance;
            }
            if (CanRun())
            {
                if (Save_Manager.instance.data.Scenes.Monoliths.Enable_MaxStability)
                {
                    __instance.maxBonusStablity = (int)
                        Save_Manager.instance.data.Scenes.Monoliths.MaxStability;
                }
                if (Save_Manager.instance.data.Scenes.Monoliths.Enable_MaxStabilityOnStart)
                {
                    __instance.bonusStablity = __instance.maxBonusStablity;
                }
                if (Save_Manager.instance.data.Scenes.Monoliths.Enable_MobsDefeatOnStart)
                {
                    __instance.enemiesDefeated = Save_Manager
                        .instance
                        .data
                        .Scenes
                        .Monoliths
                        .MobsDefeatOnStart;
                }
                if (Save_Manager.instance.data.Scenes.Monoliths.Enable_ObjectiveReveal)
                {
                    __instance.objectiveRevealThresholdModifier = float.MaxValue;
                }
                if (
                    (Save_Manager.instance.data.Scenes.Monoliths.Enable_DropShadeGauntlet)
                    && (!Refs_Manager.player_actor.IsNotNullOrDestroyed())
                )
                {
                    //Drop woven echoes on start
                    __instance.DropShadeGauntletWovenEchoForAllPlayersInEcho(
                        Refs_Manager.player_actor.gameObject.transform.position,
                        0
                    );
                }
                /*if (Save_Manager.instance.data.Scenes.Monoliths.Enable_CompleteObjective)
                {
                    //Objectives Quest
                    Main.logger_instance?.Msg("MonolithZoneManager.questsThatCompleteZone count = " + __instance.questsThatCompleteZone.Count);
                    foreach (Il2Cpp.Quest quest in __instance.questsThatCompleteZone)
                    {
                        Main.logger_instance?.Msg("Complete quest : " + quest.name);
                        quest.completeQuest(Refs_Manager.player_actor);
                    }

                    //Spawn, move and kill objective enemies
                    Main.logger_instance?.Msg("MonolithZoneManager.objectiveEnemies count = " + __instance.objectiveEnemies.Count);
                    foreach (Dying dying in __instance.objectiveEnemies)
                    {
                        Main.logger_instance?.Msg("Move enemie (" + dying.name + ") to Player");
                        dying.gameObject.transform.position = Refs_Manager.player_actor.transform.position;
                        Main.logger_instance?.Msg("Kill enemie (" + dying.name + ")");
                        dying.die();
                    }

                    //Spawn, move and kill all enemies
                    Main.logger_instance?.Msg("MonolithZoneManager.spawners count = " + __instance.spawners.Count);
                    foreach (Spawner spawner in __instance.spawners)
                    {
                        Main.logger_instance?.Msg("Spawn enemies from : " + spawner.name);
                        spawner.spawn();
                        Main.logger_instance?.Msg(spawner.name + " contain " + spawner.spawnedActors.Count + " enemies");
                        foreach (Actor actor in spawner.spawnedActors)
                        {
                            Main.logger_instance?.Msg("Move enemie (" + actor.name + ") to Player");
                            actor.gameObject.transform.position = Refs_Manager.player_actor.transform.position;
                            Main.logger_instance?.Msg("Kill enemie (" + actor.name + ")");
                            actor.gameObject.GetComponent<Dying>().die();
                        }
                    }

                    //Kill all spanwed enemies
                    Main.logger_instance?.Msg("MonolithZoneManager.spawnedEntities count = " + __instance.spawnedEntities.Count);
                    foreach (Dying dying in __instance.spawnedEntities)
                    {
                        Main.logger_instance?.Msg("Move enemie (" + dying.name + ") to Player");
                        dying.gameObject.transform.position = Refs_Manager.player_actor.transform.position;
                        Main.logger_instance?.Msg("Kill enemie (" + dying.name + ")");
                        dying.die();
                    }

                    //Objective Tomb
                    if (!__instance.TombManager.IsNullOrDestroyed())
                    {
                        Main.logger_instance?.Msg("Unlock tomb");
                        __instance.TombManager.AccessState = Il2CppLE.Gameplay.Tombs.TombAccessState.Unlocked;

                        Main.logger_instance?.Msg("Get entrance location");
                        bool entrance_found = false;
                        Vector3 entrance_location = new Vector3(0, 0, 0);
                        foreach (TombEntranceLogic _logic in Object.FindObjectsOfType<TombEntranceLogic>())
                        {
                            if (_logic.isActiveAndEnabled)
                            {
                                entrance_location = _logic.GetEntranceLocation();
                                entrance_found = true;
                                break;
                            }
                        }
                        if (entrance_found)
                        {
                            Main.logger_instance?.Msg("Move player to tomb entrance");
                            Refs_Manager.player_actor.gameObject.transform.position = entrance_location;

                            //Enter tomb

                            //Spawn and kill boss //Move player to boss spawner
                            if (!__instance.TombManager.BossSpawner.IsNotNullOrDestroyed())
                            {
                                Main.logger_instance?.Msg("Try to spawn Boss");
                                __instance.TombManager.BossSpawner.spawn();
                                foreach (Actor actor in __instance.TombManager.BossSpawner.spawnedActors)
                                {
                                    Main.logger_instance?.Msg("Try to move Boss : " + actor.name + " to Player");
                                    actor.gameObject.transform.position = Refs_Manager.player_actor.transform.position;
                                    Main.logger_instance?.Msg("Try to kill Boss : " + actor.name);
                                    actor.gameObject.GetComponent<Dying>().die();
                                }
                            }
                        }
                        else { Main.logger_instance?.Warning("Tomb entrance not found"); }
                    }
                    
                    try
                    {
                        Main.logger_instance?.Msg("Try to complete objective");
                        __instance.objectiveComplete = true;
                        __instance.runComplete = true;
                        //__instance.CompleteObjective();
                    }
                    catch { Main.logger_instance?.Error("Error when trying to complete objective"); }
                }*/
            }
        }
    }
}
