using Il2Cpp;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastEpoch_Hud.Scripts.Mods.Teleport;

[RegisterTypeInIl2Cpp]
public class Teleport_ToScene : MonoBehaviour
{
    public static Teleport_ToScene instance { get; private set; }

    public Teleport_ToScene(System.IntPtr ptr)
        : base(ptr) { }

    void Awake()
    {
        instance = this;
    }

    public static void StartTpToScene(string scene_name)
    {
        if (instance.IsNullOrDestroyed())
        {
            Main.logger_instance?.Error("Teleport is not running");
            return;
        }
        instance.Begin(scene_name);
    }

    static bool TryGetGate(string scene_name, out byte gate)
    {
        gate = 0;
        bool found = false;
        UIWaypoint[] pins = Resources.FindObjectsOfTypeAll<UIWaypoint>();
        if (pins == null)
        {
            return false;
        }
        foreach (UIWaypoint pin in pins)
        {
            if (
                (pin.IsNullOrDestroyed())
                || (pin.noWaypointInScene)
                || (pin.sceneName != scene_name)
            )
            {
                continue;
            }
            gate = pin.gate;
            found = true;
            if (pin.gate == 0)
            {
                break;
            }
        }
        return found;
    }

    void Begin(string scene_name)
    {
        if (
            (string.IsNullOrEmpty(scene_name)) || (SceneManager.GetActiveScene().name == scene_name)
        )
        {
            return;
        }
        byte gate = 0;
        TryGetGate(scene_name, out gate);
        try
        {
            if (
                (!Refs_Manager.player_data.IsNullOrDestroyed())
                && (!Refs_Manager.player_data.UnlockedWaypointScenes.IsNullOrDestroyed())
                && (!Refs_Manager.player_data.UnlockedWaypointScenes.Contains(scene_name))
            )
            {
                Refs_Manager.player_data.UnlockedWaypointScenes.Add(scene_name);
            }
            GamePauseMenu.Resume();
            if (
                (!Refs_Manager.game_uibase.IsNullOrDestroyed())
                && (Refs_Manager.game_uibase.IsWorldMapPanelOpen())
            )
            {
                Refs_Manager.game_uibase.closeMap();
            }
            if (!Il2CppLE.Networking.PlayerStore.IsLocalUserIdentityValid())
            {
                Main.logger_instance?.Error("Teleport player is missing");
                return;
            }
            Il2Cpp.BaseTransitionService travel = Il2CppLE
                .Services
                .ServiceProvider
                .TransitionService;
            if (travel == null)
            {
                Main.logger_instance?.Error("Teleport service is missing");
                return;
            }
            Main.logger_instance?.Msg("Teleport -> " + scene_name);
            travel.Waypoint(
                Il2CppLE.Networking.PlayerStore.LocalUserIdentity,
                Il2CppLE.Services.Models.TransitionFadeType.ToBlack,
                scene_name,
                gate
            );
        }
        catch (System.Exception ex)
        {
            Main.logger_instance?.Error("Teleport failed: " + ex.Message);
        }
    }
}
