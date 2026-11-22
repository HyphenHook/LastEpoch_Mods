using System.IO;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Data;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Bank;

[RegisterTypeInIl2Cpp]
public partial class Bank_Quad : MonoBehaviour
{
    public Bank_Quad(System.IntPtr ptr)
        : base(ptr) { }

    public static Bank_Quad instance { get; private set; }

    public static int character_index = -1;
    public static int backup_active_tab = -1;
    static int gridRefreshFrames = 0;
    public static Vector2Int default_size = new Vector2Int(12, 17);
    public static Sprite default_grid = null;
    public static Vector2Int quad_size = new Vector2Int(24, 34);
    public static Sprite quad_grid = null;
    public static UIPanel stash_panel = null;
    public static StashItemContainer stash_item_container = null;
    public static StashItemContainerUI stash_item_container_ui = null;
    public static Image stash_grid_image = null;
    static Image quad_overlay = null;
    static Image.Type default_grid_type = Image.Type.Simple;
    static bool default_grid_preserve_aspect = false;
    static bool saved_grid_image_settings = false;
    private static readonly System.Collections.Generic.Dictionary<
        System.IntPtr,
        bool[]
    > occupied_slots = new System.Collections.Generic.Dictionary<System.IntPtr, bool[]>();
    public static ItemContainersManager item_contenairs_manager = null;

    //configure
    public static ConfigureTabUI configure_tab_ui = null;
    public static GameObject QuadStash_obj = null;
    public static string toggle_str = "Quad Stash";
    public static string toggle_explain_str =
        "Items that no longer fit move to your inventory, or drop at your feet.";
    public static Toggle configure_stash_toggle = null;
    public static TextMeshProUGUI configure_stash_toggle_title = null;
    public static TextMeshProUGUI configure_stash_toggle_explanation = null;
    public static string configure_stash_name_backup = "";
    public static bool open_configure = false;
    static bool ids_migrated = false;
    static bool icons_use_quad_size = false;
    static bool have_native_slot = false;
    static Vector2 native_slot_size = Vector2.zero;

    void Awake()
    {
        instance = this;
    }

    void Update()
    {
        if (!Refs_Manager.game_uibase.IsNullOrDestroyed())
        {
            if (!Scenes.IsGameScene())
            {
                if (!stash_panel.IsNullOrDestroyed())
                {
                    stash_panel = null;
                } //Reset

                if (
                    (Refs_Manager.game_uibase.characterSelectOpen)
                    && (!Refs_Manager.game_uibase.characterSelectPanel.IsNullOrDestroyed())
                )
                {
                    GameObject char_selection_game_object = Refs_Manager
                        .game_uibase
                        .characterSelectPanel
                        .instance;
                    if (!char_selection_game_object.IsNullOrDestroyed())
                    {
                        CharacterSelect char_select =
                            char_selection_game_object.GetComponent<CharacterSelect>();
                        LocalCharacterSlots local_slots =
                            char_selection_game_object.GetComponent<LocalCharacterSlots>();

                        if (
                            (!char_select.IsNullOrDestroyed()) && (!local_slots.IsNullOrDestroyed())
                        )
                        {
                            if (
                                char_select.currentState
                                == CharacterSelect.CharacterSelectState.LoadCharacter
                            )
                            {
                                int index = char_select.SelectedCharacterIndex;
                                if (
                                    (index > -1)
                                    && (index != character_index)
                                    && (index < local_slots.characterSlots.Count)
                                )
                                {
                                    character_index = index;
                                    Cycle cycle = local_slots.characterSlots[index].Cycle;
                                    string solo_char_name = "";
                                    StashType stashType = StashType.Softcore;

                                    if (local_slots.characterSlots[index].SoloChallenge)
                                    {
                                        solo_char_name = local_slots
                                            .characterSlots[index]
                                            .CharacterName;
                                        Save.Data.path =
                                            Save.Data.base_path
                                            + cycle.ToString()
                                            + @"\"
                                            + solo_char_name
                                            + @"\";
                                    }
                                    else
                                    {
                                        if (local_slots.characterSlots[index].Hardcore)
                                        {
                                            stashType = StashType.Hardcore;
                                        }
                                        else
                                        {
                                            stashType = StashType.Softcore;
                                        }
                                        Save.Data.path =
                                            Save.Data.base_path
                                            + cycle.ToString()
                                            + @"\"
                                            + stashType.ToString()
                                            + @"\";
                                    }
                                    Save.Data.Load();
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                //Get Refs
                if (stash_panel.IsNullOrDestroyed() && !StashPanelUI.Instance.IsNullOrDestroyed())
                {
                    stash_panel = StashPanelUI.Instance.GetComponent<UIPanel>();
                    if (stash_panel.IsNullOrDestroyed())
                    {
                        stash_panel = StashPanelUI.Instance.GetComponentInParent<UIPanel>();
                    }
                }
                if (
                    (!stash_panel.IsNullOrDestroyed())
                    && ( /*(stash_item_container_ui.IsNullOrDestroyed()) ||*/
                        (stash_grid_image.IsNullOrDestroyed()) || (default_grid.IsNullOrDestroyed())
                    )
                )
                {
                    if (!stash_panel.instance.IsNullOrDestroyed())
                    {
                        GameObject left_obj = Functions.GetChild(
                            stash_panel.instance,
                            "left-container"
                        );
                        if (!left_obj.IsNullOrDestroyed())
                        {
                            /*if (stash_item_container_ui.IsNullOrDestroyed())
                            {
                                GameObject stash_obj = Functions.GetChild(left_obj, "Stash");
                                if (!stash_obj.IsNullOrDestroyed()) {  stash_item_container_ui = stash_obj.GetComponent<StashItemContainerUI>(); }
                            }*/
                            if (stash_grid_image.IsNullOrDestroyed())
                            {
                                GameObject grid_obj = Functions.GetChild(
                                    left_obj,
                                    "grid-img",
                                    false
                                );
                                if (grid_obj.IsNullOrDestroyed())
                                {
                                    grid_obj = Functions.FindDescendant(left_obj, "grid-img");
                                }
                                if (!grid_obj.IsNullOrDestroyed())
                                {
                                    stash_grid_image = grid_obj.GetComponent<Image>();
                                }
                            }
                            if (
                                (!stash_grid_image.IsNullOrDestroyed())
                                && (default_grid.IsNullOrDestroyed())
                            )
                            {
                                default_grid = stash_grid_image.activeSprite;
                                default_grid_type = stash_grid_image.type;
                                default_grid_preserve_aspect = stash_grid_image.preserveAspect;
                                saved_grid_image_settings = true;
                                Object.DontDestroyOnLoad(default_grid);
                            }
                        }
                    }
                }
                if (
                    (!Hud_Manager.asset_bundle.IsNullOrDestroyed())
                    && (quad_grid.IsNullOrDestroyed())
                )
                {
                    foreach (string name in Hud_Manager.asset_bundle.GetAllAssetNames())
                    {
                        if (name.Contains("/quadstash/"))
                        {
                            if ((Functions.Check_Texture(name)) && (name.Contains("quad_grid")))
                            {
                                Texture2D texture = Hud_Manager
                                    .asset_bundle.LoadAsset(name)
                                    .TryCast<Texture2D>();
                                quad_grid = Sprite.Create(
                                    texture,
                                    new Rect(0, 0, texture.width, texture.height),
                                    Vector2.zero
                                );
                                Object.DontDestroyOnLoad(quad_grid);
                            }
                        }
                    }
                }

                //Update UI
                if (
                    (!stash_panel.IsNullOrDestroyed())
                    && (!stash_item_container.IsNullOrDestroyed())
                )
                {
                    if (stash_panel.isOpen)
                    {
                        // Rebuild after the game finishes selecting the tab. Doing it inside the
                        // tab-change callback uses the previous container and leaves the icons wrong.
                        bool tabChanged =
                            backup_active_tab != stash_item_container.CurrentlyActiveTab;
                        if ((tabChanged || (gridRefreshFrames > 0)) && RefreshActiveGrid())
                        {
                            backup_active_tab = stash_item_container.CurrentlyActiveTab;
                            if (gridRefreshFrames > 0)
                            {
                                gridRefreshFrames--;
                            }
                        }
                    }
                    else
                    {
                        backup_active_tab = -1;
                    }
                }
                if (!configure_tab_ui.IsNullOrDestroyed())
                {
                    if ((open_configure) && (configure_tab_ui.gameObject.active)) //DoOnce
                    {
                        open_configure = false;
                        configure_stash_name_backup = configure_tab_ui.nameInputTMP.text; //set backup name
                        if (!configure_stash_toggle_title.IsNullOrDestroyed())
                        {
                            if (configure_stash_toggle_title.text != toggle_str)
                            {
                                configure_stash_toggle_title.text = toggle_str;
                            }
                        }
                        if (!configure_stash_toggle_explanation.IsNullOrDestroyed())
                        {
                            if (configure_stash_toggle_explanation.text != toggle_explain_str)
                            {
                                configure_stash_toggle_explanation.text = toggle_explain_str;
                            }
                        }
                        if (
                            (!Save.Data.UserTabs.IsNullOrDestroyed())
                            && (!configure_stash_toggle.IsNullOrDestroyed())
                        )
                        {
                            configure_stash_toggle.isOn = Get.IsQuadStash(
                                configure_tab_ui.selectedTabID
                            );
                        }
                    }
                }
            }
        }
    }

    private static void PrepareToggle(Toggle toggle)
    {
        if (toggle.IsNullOrDestroyed())
        {
            return;
        }
        // The cloned priority row belongs to a toggle group that turns itself back on.
        toggle.group = null;
        toggle.onValueChanged = new Toggle.ToggleEvent();
        toggle.interactable = true;
    }
}
