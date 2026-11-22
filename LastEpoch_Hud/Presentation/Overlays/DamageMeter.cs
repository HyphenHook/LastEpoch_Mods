using HarmonyLib;
using Il2Cpp;
using Il2CppLE.AssetBundles;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.UI;

[RegisterTypeInIl2Cpp]
public partial class DamageMeter : MonoBehaviour
{
    public static DamageMeter instance { get; private set; }

    public DamageMeter(System.IntPtr ptr)
        : base(ptr) { }

    //Main
    public static GameObject DamageMeter_prefab = null;
    public static GameObject DamageMeter_obj = null;
    public static GameObject DamageMeter_content = null;
    public static TextMeshProUGUI MenuText = null;
    public static Button OnOff_btn = null;
    public static bool On = false;
    public static GameObject Reset_obj = null;
    public static Button Reset_btn = null;
    public static Image OnOff_Image = null;
    public static Sprite On_sprite = null;
    public static Sprite Off_sprite = null;

    //Skills
    public static GameObject Skill_prefab = null;
    public static System.Collections.Generic.List<Ability> Abilities =
        new System.Collections.Generic.List<Ability>();
    public static System.Collections.Generic.List<Skill> Skills =
        new System.Collections.Generic.List<Skill>();
    public static float TotalDamageDeal = 0f;
    public static float RecordedSeconds = 0f; //Game time spent recording since the first hit, pauses excluded
    public static Text Title_Text = null;
    public const int DamageType_Flat = 1;
    public const int DamageType_Dps = 2;

    //Settings
    public static GameObject Settings_panel = null;
    public static GameObject Settings_obj = null;
    public static Button Settings_btn = null;
    public static Dropdown DamageType_Dropdown = null;
    public static Toggle SeparateDot_Toggle = null;

    //Details
    public static GameObject Details_TopContent_prefab = null;
    public static GameObject Details_BottomContent_prefab = null;
    public static GameObject Details_panel = null;
    public static GameObject Details_TopCircleContent = null;
    public static GameObject Details_TopContent = null;
    public static GameObject Details_BottomCircleContent = null;
    public static GameObject Details_BottomContent = null;
    public static System.Collections.Generic.List<GameObject> details_skills =
        new System.Collections.Generic.List<GameObject>();
    public static string Details_TopSkillsName = "TopSkill_";
    public static Color[] colors =
    {
        Color.green,
        Color.blue,
        Color.gray,
        Color.cyan,
        Color.green,
        Color.white,
        Color.grey,
        Color.magenta,
        Color.black,
        Color.yellow,
    };

    public static int Frames = 30; //Update append every x frames (to avoid too much performance cost)
    public static int Current_Frame = 0;

    void Awake()
    {
        instance = this;
        UI.Reset();
    }

    static KeyCode ToggleKey()
    {
        // Saves written before this bind existed load it as None.
        KeyCode key = KeyCode.None;
        if (!Save_Manager.instance.IsNullOrDestroyed())
        {
            key = Save_Manager.instance.data.KeyBinds.DamageMeter;
        }
        return key == KeyCode.None ? KeyCode.F7 : key;
    }

    static bool BottomScreenReady()
    {
        return !Refs_Manager.game_uibase.IsNullOrDestroyed()
            && !Refs_Manager.game_uibase.bottomScreenMenu.IsNullOrDestroyed();
    }

    static void InitWithBottomScreen()
    {
        if (!Assets.Loaded())
        {
            Assets.Load();
        }
        if (!Assets.Loaded())
        {
            return;
        }
        if (Abilities.Count == 0)
        {
            foreach (Ability ab in Resources.FindObjectsOfTypeAll<Ability>())
            {
                if (!Abilities.Contains(ab))
                {
                    Abilities.Add(ab);
                }
            }
            Ability[] mjolnerAbilities =
                Items.CustomItems.Mjolner.Items_Mjolner.GetTriggerAbilities();
            if (mjolnerAbilities != null)
            {
                foreach (Ability ab in mjolnerAbilities)
                {
                    if (!ab.IsNullOrDestroyed() && !Abilities.Contains(ab))
                    {
                        Abilities.Add(ab);
                    }
                }
            }
        }
        if (!UI.Initialized)
        {
            UI.Init();
        }
    }

    static readonly System.Collections.Generic.Dictionary<string, LoadRef<Sprite>> ability_icons =
        new System.Collections.Generic.Dictionary<string, LoadRef<Sprite>>();

    void Update()
    {
        if (Scenes.IsGameScene() && !UI.Initialized && BottomScreenReady())
        {
            InitWithBottomScreen();
        }
        if (!Assets.Loaded())
        {
            return;
        }
        if (Scenes.IsGameScene())
        {
            if (!UI.Initialized)
            {
                return;
            }
            if (!MenuText.IsNullOrDestroyed())
            {
                MenuText.text = "Damage Meter";
            }
            if (!DamageMeter_obj.IsNullOrDestroyed())
            {
                if (Input.GetKeyDown(ToggleKey()))
                {
                    DamageMeter_obj.active = !DamageMeter_obj.active;
                }
                if (!DamageMeter_obj.active)
                {
                    On = false;
                }
                if (On && Skills.Count > 0)
                {
                    RecordedSeconds += Time.deltaTime;
                }
                if (On)
                {
                    UI.Update();
                }
            }
            if (!OnOff_Image.IsNullOrDestroyed())
            {
                if (On)
                {
                    OnOff_Image.sprite = On_sprite;
                }
                else
                {
                    OnOff_Image.sprite = Off_sprite;
                }
            }
            if (!Reset_obj.IsNullOrDestroyed())
            {
                if (Skills.Count > 0)
                {
                    Reset_obj.active = true;
                }
                else
                {
                    Reset_obj.active = false;
                }
            }
        }
        else
        {
            On = false;
            Abilities = new System.Collections.Generic.List<Ability>();
            if (!DamageMeter_obj.IsNullOrDestroyed())
            {
                if (DamageMeter_obj.active)
                {
                    DamageMeter_obj.active = false;
                }
            }
            UI.Initialized = false;
        }
    }
}
