using System;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.QualityOfLife;
using LastEpoch_Hud.Scripts.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace LastEpoch_Hud.Scripts.Mods.Teleport;

[RegisterTypeInIl2Cpp]
public class TravelAnywhere : MonoBehaviour
{
    public TravelAnywhere(IntPtr ptr)
        : base(ptr) { }

    public static TravelAnywhere Instance { get; private set; }
    public static bool Busy => !Instance.IsNullOrDestroyed() && Instance.progress.Busy;
    public static string Status { get; private set; } = "Use the world map to travel when enabled.";

    readonly SceneTravelProgress progress = new();
    Scene source;
    Scene target;
    Actor traveller;
    Vector3 origin;
    int gate;
    AsyncOperation loading;
    AsyncOperation cleanup;
    bool touchedPlayer;
    bool movedPlayer;
    bool slowCleanupReported;
    float nextPlace;
    float nextCleanup;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        TravelMapWaypoints.RestoreAll(true);
        if (Instance == this)
            Instance = null;
    }

    public static bool StartTravel(string scene, int entrance = 0)
    {
        if (Instance.IsNullOrDestroyed())
        {
            Status = "Travel is not ready.";
            return false;
        }
        try
        {
            return Instance.Begin(scene, entrance);
        }
        catch (Exception ex)
        {
            Instance.Fail(ex.Message);
            return false;
        }
    }

    bool Begin(string scene, int entrance)
    {
        if (progress.Busy)
            return false;
        if (!ModSettings.TravelAnywhere.Enabled.Value)
        {
            Status = "Enable Travel Anywhere first.";
            return false;
        }
        if (
            !Scenes.IsGameScene()
            || Refs_Manager.player_actor.IsNullOrDestroyed()
            || Refs_Manager.player_data.IsNullOrDestroyed()
            || PlayerSpawnManager.instance.IsNullOrDestroyed()
            || !Il2CppLE.Networking.PlayerStore.IsLocalUserIdentityValid()
        )
        {
            Status = "Travel is not ready.";
            return false;
        }
        TravelDestinations.Refresh();
        if (TravelDestinations.Find(scene) == null)
        {
            Status = "This area is not available for direct travel.";
            return false;
        }
        source = SceneManager.GetActiveScene();
        target = SceneManager.GetSceneByName(scene);
        if (!source.IsValid() || !source.isLoaded || scene == source.name)
        {
            Status = "You are already in this area, or the current scene is unavailable.";
            return false;
        }
        // Avoid duplicate additive copies and ambiguous scene-name lookups.
        if (target.IsValid() && target.isLoaded)
        {
            Status = "This destination is already loaded.";
            return false;
        }
        traveller = Refs_Manager.player_actor;
        origin = traveller.position();
        gate = Math.Max(0, entrance);
        touchedPlayer = movedPlayer = slowCleanupReported = false;
        nextCleanup = 0;
        loading = cleanup = null;
        if (!progress.Begin(source.name, scene, Time.realtimeSinceStartup))
            return false;
        Status = "Loading destination...";
        Main.logger_instance?.Msg(
            "[TravelAnywhere] Load " + source.name + " -> " + scene + "; gate=" + gate
        );
        loading = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
        if (loading == null)
        {
            Fail("Scene loading did not start.");
            return false;
        }
        return true;
    }

    void Update()
    {
        try
        {
            TravelMapWaypoints.Tick();
            HandleMapClick();
            TickTravel();
        }
        catch (Exception ex)
        {
            if (
                progress.Phase == SceneTravelPhase.Loading
                || progress.Phase == SceneTravelPhase.Placing
            )
                Fail(ex.Message);
            ErrorLog.Report(ex, "Travel Anywhere update");
        }
    }

    static void HandleMapClick()
    {
        if (
            Busy
            || !ModSettings.TravelAnywhere.Enabled.Value
            || !Input.GetMouseButtonDown(1)
            || Refs_Manager.game_uibase.IsNullOrDestroyed()
            || !Refs_Manager.game_uibase.IsWorldMapPanelOpen()
        )
            return;
        var events = EventSystem.current;
        if (events.IsNullOrDestroyed())
            return;
        var pointer = new PointerEventData(events)
        {
            position = Input.mousePosition,
            button = PointerEventData.InputButton.Right,
        };
        var hits = new Il2CppSystem.Collections.Generic.List<RaycastResult>();
        events.RaycastAll(pointer, hits);
        // Use the frontmost graphic only. A popup above the map must block travel.
        if (hits.Count == 0 || hits[0].gameObject.IsNullOrDestroyed())
            return;
        var pin = hits[0].gameObject.GetComponentInParent<UIWaypoint>();
        if (!pin.IsNullOrDestroyed())
        {
            TravelMapWaypoints.Prepare(pin);
            StartTravel(pin.sceneName, pin.gate);
        }
    }

    void TickTravel()
    {
        double now = Time.realtimeSinceStartup;
        if (progress.Phase == SceneTravelPhase.Loading)
        {
            if (progress.Expired(now, 30))
                Fail("Destination loading timed out.");
            else if (loading != null && loading.isDone)
            {
                target = SceneManager.GetSceneByName(progress.Target);
                if (!target.IsValid() || !target.isLoaded)
                    Fail("The loaded destination is unavailable.");
                else
                {
                    progress.Loaded(now);
                    Status = "Placing player...";
                    nextPlace = 0;
                }
            }
        }
        if (progress.Phase == SceneTravelPhase.Placing)
        {
            if (progress.Expired(now, 8))
                Fail("No usable player spawn was found.");
            else if (Time.realtimeSinceStartup >= nextPlace)
            {
                nextPlace = Time.realtimeSinceStartup + .25f;
                TryPlace(now);
            }
        }
        if (progress.Phase == SceneTravelPhase.UnloadingSource)
        {
            if (!source.isLoaded)
            {
                progress.Completed(true);
                Status = "Travel complete.";
                Main.logger_instance?.Msg("[TravelAnywhere] Complete -> " + progress.Target);
                ClearNativeRefs();
            }
            else if (!slowCleanupReported && progress.Expired(now, 30))
            {
                slowCleanupReported = true;
                Status = "At destination; previous scene cleanup is still pending.";
                Main.logger_instance?.Warning(
                    "[TravelAnywhere] Source cleanup still pending; further travel blocked."
                );
            }
            if (source.isLoaded && cleanup == null && Time.realtimeSinceStartup >= nextCleanup)
            {
                nextCleanup = Time.realtimeSinceStartup + 1;
                cleanup = SceneManager.UnloadSceneAsync(source);
            }
        }
        if (progress.Phase == SceneTravelPhase.Recovering)
            Recover();
    }

    void TryPlace(double now)
    {
        if (traveller.IsNullOrDestroyed() || !source.isLoaded)
        {
            Fail("The original player or scene changed during travel.");
            return;
        }
        var local = PlayerFinder.getPlayerActor();
        if (local.IsNullOrDestroyed())
            return;
        if (local.Pointer != traveller.Pointer)
        {
            Fail("The local player changed during direct travel.");
            return;
        }
        var manager = PlayerSpawnManager.instance;
        if (manager.IsNullOrDestroyed())
            return;
        if (!SceneManager.SetActiveScene(target))
            return;
        Scenes.SceneName = target.name;
        touchedPlayer = true;
        // Compiled April release used these exact optional flags: false, true, false.
        bool placed = manager.TryPlacePlayerAtSpawn(
            traveller,
            source.name,
            target.name,
            gate,
            out PlayerSpawn chosen,
            false,
            true,
            false
        );
        if (!placed || chosen.IsNullOrDestroyed())
            return;
        local = PlayerFinder.getPlayerActor();
        if (local.IsNullOrDestroyed() || local.Pointer != traveller.Pointer)
        {
            Fail("The local player changed during spawn placement.");
            return;
        }
        var playerObject = traveller.gameObject;
        if (playerObject.scene.handle == source.handle)
        {
            // Never move an arbitrary scene hierarchy containing the player.
            if (!playerObject.transform.parent.IsNullOrDestroyed())
            {
                Fail("Player ownership prevented source cleanup.");
                return;
            }
            SceneManager.MoveGameObjectToScene(playerObject, target);
            movedPlayer = true;
        }
        GamePauseMenu.Resume();
        if (
            !Refs_Manager.game_uibase.IsNullOrDestroyed()
            && Refs_Manager.game_uibase.IsWorldMapPanelOpen()
        )
            Refs_Manager.game_uibase.closeMap();
        if (!progress.Placed(true, now) || !progress.CanUnloadSource)
            return;
        Status = "Finishing travel...";
        // Unity unloads cannot be cancelled. Keep observing until the source is gone.
    }

    void Fail(string reason)
    {
        if (
            progress.Phase == SceneTravelPhase.Loading
            && loading == null
            && !touchedPlayer
            && !movedPlayer
        )
        {
            target = SceneManager.GetSceneByName(progress.Target);
            var current = SceneManager.GetActiveScene();
            var local = PlayerFinder.getPlayerActor();
            bool sourceRetained =
                source.IsValid()
                && source.isLoaded
                && current.handle == source.handle
                && !traveller.IsNullOrDestroyed()
                && !local.IsNullOrDestroyed()
                && local.Pointer == traveller.Pointer;
            if (
                progress.LoadRejected(reason, sourceRetained, !target.IsValid() || !target.isLoaded)
            )
            {
                Status = "Travel failed; original area retained. See the mod log.";
                Main.logger_instance?.Warning(
                    "[TravelAnywhere] "
                        + reason
                        + "; no load started; source retained; travel guard released."
                );
                ClearNativeRefs();
                return;
            }
        }
        if (!progress.Recover(reason, Time.realtimeSinceStartup))
            return;
        Status = "Travel failed; restoring the original area...";
        Main.logger_instance?.Warning("[TravelAnywhere] " + reason + "; source retained.");
    }

    void Recover()
    {
        bool safeReturn = false;
        if (source.IsValid() && source.isLoaded)
        {
            safeReturn = SceneManager.SetActiveScene(source);
            if (safeReturn)
            {
                Scenes.SceneName = source.name;
                if (!traveller.IsNullOrDestroyed() && touchedPlayer)
                {
                    if (movedPlayer)
                        SceneManager.MoveGameObjectToScene(traveller.gameObject, source);
                    traveller.transform.position = origin;
                    movedPlayer = touchedPlayer = false;
                }
            }
        }
        else if (Scenes.IsCharacterSelection() || Scenes.SceneName == "Login")
            safeReturn = true;
        if (!safeReturn)
            return;
        // LoadSceneAsync has no cancellation API. Wait for a timed-out load to finish
        // before removing its destination; retain the busy guard throughout cleanup.
        if (loading != null && !loading.isDone)
        {
            Status = "Original area retained; waiting for destination cleanup.";
            return;
        }
        target = SceneManager.GetSceneByName(progress.Target);
        if (!target.IsValid() || !target.isLoaded)
        {
            progress.Recovered(true, true);
            Status = "Travel failed; original area retained. See the mod log.";
            ClearNativeRefs();
            return;
        }
        if (!traveller.IsNullOrDestroyed() && traveller.gameObject.scene.handle == target.handle)
            return;
        var currentPlayer = PlayerFinder.getPlayerActor();
        if (
            !currentPlayer.IsNullOrDestroyed()
            && currentPlayer.gameObject.scene.handle == target.handle
        )
        {
            Status = "Recovery paused because the local player changed. See the mod log.";
            return;
        }
        if (cleanup == null && Time.realtimeSinceStartup >= nextCleanup)
        {
            nextCleanup = Time.realtimeSinceStartup + 1;
            cleanup = SceneManager.UnloadSceneAsync(target);
        }
    }

    void ClearNativeRefs()
    {
        loading = cleanup = null;
        traveller = null;
    }

    [HarmonyPatch(typeof(UIWaypoint), "LoadWaypointScene")]
    public class MapTravelPatch
    {
        [HarmonyPrefix]
        static bool Prefix(UIWaypoint __instance)
        {
            // Also block native waypoint requests after the option is turned off
            // while an additive load/cleanup is still pending.
            if (Busy)
                return false;
            if (
                !ModSettings.TravelAnywhere.Enabled.Value
                && TravelMapWaypoints.BlocksStaleNativeAction(__instance)
            )
            {
                Status = "Enable Travel Anywhere first.";
                return false;
            }
            if (
                !ModSettings.TravelAnywhere.Enabled.Value
                || !Scenes.IsGameScene()
                || __instance.IsNullOrDestroyed()
                || Refs_Manager.game_uibase.IsNullOrDestroyed()
                || !Refs_Manager.game_uibase.IsWorldMapPanelOpen()
            )
                return true;
            try
            {
                TravelDestinations.Refresh();
                if (TravelDestinations.Find(__instance.sceneName) == null)
                    return true;
                StartTravel(__instance.sceneName, __instance.gate);
                return false;
            }
            catch (Exception ex)
            {
                ErrorLog.Report(ex, "Travel Anywhere map click");
                return false;
            }
        }
    }
}
