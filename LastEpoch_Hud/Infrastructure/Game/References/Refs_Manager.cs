using HarmonyLib;
using Il2Cpp;
using Il2CppItemFiltering;
using Il2CppLE.Factions;
using Il2CppLE.Services.Visuals;
using MelonLoader;
using UnityEngine;

namespace LastEpoch_Hud.Scripts;

[RegisterTypeInIl2Cpp]
public class Refs_Manager : MonoBehaviour
{
    public Refs_Manager(System.IntPtr ptr)
        : base(ptr) { }

    public static Refs_Manager instance { get; private set; }

    public static bool online = true;

    public static UIBase game_uibase = null;
    public static EpochInputManager epoch_input_manager = null; //Use to block input
    public static CharacterSelect character_select = null;
    public static SceneList scene_list = null;
    public static InventoryPanelUI InventoryPanelUI = null;
    public static EternityCachePanelUI EternityCachePanelUI = null;
    public static GameObject BlessingsPanel = null;
    public static Actor player_actor = null;
    public static ActorVisuals player_visuals = null;
    public static Il2CppLE.Data.CharacterData player_data = null;
    public static CharacterDataTracker player_data_tracker = null;
    public static PlayerHealth player_health = null;
    public static HealthPotion health_potion = null;
    public static Stats player_stats = null;
    public static GoldTracker player_gold_tracker = null;
    public static LocalTreeData player_treedata = null;
    public static CharacterClassList character_class_list = null;
    public static ExperienceTracker exp_tracker = null;
    public static GroundItemManager ground_item_manager = null;
    public static ItemContainersManager item_containers_manager = null;
    public static ItemList item_list = null;
    public static UniqueList unique_list = null;
    public static SetBonusesList set_bonuses_list = null;
    public static QuestList quest_list = null;
    public static PlayerQuestListHolder player_quest_list = null;
    public static ItemFilterManager filter_manager = null;
    public static CameraManager camera_manager = null;
    public static CraftingSlotManager craft_slot_manager = null;
    public static UIPanel craft_materials_holder = null;
    public static CraftingPanelUI crafting_panel_ui = null;
    public static ProtectionClass player_protection_class = null;
    public static GlobalDataTracker player_golbal_data_tracker = null;
    public static MonolithZoneManager monolith_zone_manager = null;
    public static MovingPlayer player_moving = null;
    public static AbilityManager ability_manager = null;
    public static FactionTracker faction_tracker = null;
    float nextSlowLoad;

    void Awake()
    {
        instance = this;
    }

    void Update()
    {
        try
        {
            Tick();
        }
        catch (System.Exception ex)
        {
            ErrorLog.Report(ex, "Refs_Manager.Tick");
        }
    }

    static T TryGet<T>(System.Func<T> getter)
        where T : UnityEngine.Object
    {
        try
        {
            return getter();
        }
        catch (System.Exception)
        {
            // Game singleton getters throw until their data is loaded; LoadSlowRefs retries every 15 s.
            return null;
        }
    }

    void LoadSlowRefs()
    {
        bool missing =
            character_class_list.IsNullOrDestroyed()
            || item_list.IsNullOrDestroyed()
            || unique_list.IsNullOrDestroyed()
            || set_bonuses_list.IsNullOrDestroyed()
            || quest_list.IsNullOrDestroyed()
            || ability_manager.IsNullOrDestroyed()
            || EternityCachePanelUI.IsNullOrDestroyed();
        if (!missing || (UnityEngine.Time.unscaledTime < nextSlowLoad))
        {
            return;
        }
        nextSlowLoad = UnityEngine.Time.unscaledTime + 15f;
        if (character_class_list.IsNullOrDestroyed())
        {
            character_class_list = TryGet(CharacterClassList.get);
        }
        if (item_list.IsNullOrDestroyed())
        {
            item_list = TryGet(ItemList.get);
        }
        if (unique_list.IsNullOrDestroyed())
        {
            unique_list = TryGet(() => UniqueList.instance);
        }
        if (set_bonuses_list.IsNullOrDestroyed())
        {
            set_bonuses_list = TryGet(() => SetBonusesList.instance);
        }
        if (quest_list.IsNullOrDestroyed())
        {
            quest_list = TryGet(QuestList.get);
        }
        if (ability_manager.IsNullOrDestroyed())
        {
            ability_manager = TryGet(() => AbilityManager.instance);
        }
        if (EternityCachePanelUI.IsNullOrDestroyed())
        {
            EternityCachePanelUI = EternityCachePanelUI.instance;
        }
    }

    void Tick()
    {
        if ((game_uibase.IsNullOrDestroyed()) && (!UIBase.instance.IsNullOrDestroyed()))
        {
            game_uibase = UIBase.instance;
        }
        if (
            (epoch_input_manager.IsNullOrDestroyed())
            && (!EpochInputManager.instance.IsNullOrDestroyed())
        )
        {
            epoch_input_manager = EpochInputManager.instance;
        }
        LoadSlowRefs();
        if ((scene_list.IsNullOrDestroyed()) && (!SceneList.instance.IsNullOrDestroyed()))
        {
            scene_list = SceneList.instance;
        }
        if (
            (character_select.IsNullOrDestroyed())
            && (!CharacterSelect.instance.IsNullOrDestroyed())
        )
        {
            character_select = CharacterSelect.instance;
        }
        if ((BlessingsPanel.IsNullOrDestroyed()) && (!InventoryPanelUI.IsNullOrDestroyed()))
        {
            BlessingsPanel = InventoryPanelUI.blessingPanel;
        }

        if (Scenes.IsGameScene())
        {
            if (
                (ground_item_manager.IsNullOrDestroyed())
                && (!GroundItemManager.instance.IsNullOrDestroyed())
            )
            {
                ground_item_manager = GroundItemManager.instance;
            }
            if (
                (item_containers_manager.IsNullOrDestroyed())
                && (!ItemContainersManager.Instance.IsNullOrDestroyed())
            )
            {
                item_containers_manager = ItemContainersManager.Instance;
            }
            if (player_actor.IsNullOrDestroyed())
            {
                player_actor = PlayerFinder.getPlayerActor();
            }
            if (player_visuals.IsNullOrDestroyed())
            {
                player_visuals = PlayerFinder.getPlayerVisuals();
            }
            if (player_data.IsNullOrDestroyed())
            {
                player_data = PlayerFinder.getPlayerData();
            }
            if (player_data_tracker.IsNullOrDestroyed())
            {
                player_data_tracker = PlayerFinder.getPlayerDataTracker();
            }
            if ((faction_tracker.IsNullOrDestroyed()) && (!player_actor.IsNullOrDestroyed()))
            {
                faction_tracker = player_actor.gameObject.GetComponent<FactionTracker>();
            }
            if ((player_quest_list.IsNullOrDestroyed()) && (!player_actor.IsNullOrDestroyed()))
            {
                player_quest_list = player_actor.gameObject.GetComponent<PlayerQuestListHolder>();
            }
            if (player_health.IsNullOrDestroyed())
            {
                player_health = PlayerFinder.getLocalPlayerHealth();
            }
            if ((player_moving.IsNullOrDestroyed()) && (!player_actor.IsNullOrDestroyed()))
            {
                player_moving = player_actor.gameObject.GetComponent<MovingPlayer>();
            }
            if (
                (player_protection_class.IsNullOrDestroyed()) && (!player_actor.IsNullOrDestroyed())
            )
            {
                player_protection_class = player_actor.gameObject.GetComponent<ProtectionClass>();
            }
            if ((health_potion.IsNullOrDestroyed()) && (!player_actor.IsNullOrDestroyed()))
            {
                health_potion = player_actor.gameObject.GetComponent<HealthPotion>();
            }
            if (player_stats.IsNullOrDestroyed())
            {
                player_stats = PlayerFinder.getLocalPlayerStats();
            }
            if (exp_tracker.IsNullOrDestroyed())
            {
                exp_tracker = PlayerFinder.getExperienceTracker();
            }
            if (player_treedata.IsNullOrDestroyed())
            {
                player_treedata = PlayerFinder.getLocalTreeData();
            }
            if (player_gold_tracker.IsNullOrDestroyed())
            {
                player_gold_tracker = PlayerFinder.getLocalGoldTracker();
            }
            if (player_golbal_data_tracker.IsNullOrDestroyed())
            {
                player_golbal_data_tracker = PlayerFinder.getGlobalDataTracker();
            }
            if (
                (filter_manager.IsNullOrDestroyed())
                && (!ItemFilterManager.Instance.IsNullOrDestroyed())
            )
            {
                filter_manager = ItemFilterManager.Instance;
            }
            if (
                (camera_manager.IsNullOrDestroyed())
                && (!CameraManager.instance.IsNullOrDestroyed())
            )
            {
                camera_manager = CameraManager.instance;
            }
        }
        else
        {
            if (!player_data.IsNullOrDestroyed())
            {
                player_data = null;
            }
        }
    }

    [HarmonyPatch(typeof(InventoryPanelUI), "Awake")]
    public class InventoryPanelUI_Awake
    {
        [HarmonyPostfix]
        static void Postfix(InventoryPanelUI __instance)
        {
            InventoryPanelUI = __instance;
            if (!__instance.IsNullOrDestroyed())
            {
                BlessingsPanel = __instance.blessingPanel;
            }
        }
    }

    [HarmonyPatch(typeof(CraftingSlotManager), "Awake")]
    public class CraftingSlotManager_Awake
    {
        [HarmonyPostfix]
        static void Postfix(CraftingSlotManager __instance)
        {
            craft_slot_manager = __instance;
        }
    }

    [HarmonyPatch(typeof(CraftingMaterialsPanelUI), "Initialize")]
    public class CraftingMaterialsPanelUI_Initialize
    {
        [HarmonyPostfix]
        static void Postfix(CraftingMaterialsPanelUI __instance)
        {
            if (__instance.IsNullOrDestroyed())
            {
                return;
            }
            craft_materials_holder = __instance.GetComponent<UIPanel>();
        }
    }

    [HarmonyPatch(typeof(EternityCachePanelUI), "Awake")]
    public class EternityCachePanelUI_Awake
    {
        [HarmonyPostfix]
        static void Postfix(EternityCachePanelUI __instance)
        {
            EternityCachePanelUI = __instance;
        }
    }

    /*private static readonly System.Action<bool> Action_SetOnline = new System.Action<bool>(SetOnline);
    private static void SetOnline(bool result)
    {
        result = true;
        if (!character_select.IsNullOrDestroyed()) { result = character_select.isOnlineTabShowing; }
        if (online != result)
        {
            Main.logger_instance?.Msg("Refs Manager : Online = " + result);
            online = result;
            if (!Mods_Manager.instance.IsNullOrDestroyed()) { Mods_Manager.instance.SetActive(result); }
        }
    }*/
}
