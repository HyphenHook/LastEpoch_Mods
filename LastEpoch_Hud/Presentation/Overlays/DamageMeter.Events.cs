using HarmonyLib;
using Il2Cpp;
using Il2CppLE.AssetBundles;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.UI;

public partial class DamageMeter : MonoBehaviour
{

    public class Events
    {
        public static void Set(Button btn, UnityEngine.Events.UnityAction action)
        {
            if (!btn.IsNullOrDestroyed())
            {
                btn.onClick = new Button.ButtonClickedEvent();
                btn.onClick.AddListener(action);
            }
        }

        public static void Set_Toggle_Event(
            Toggle toggle,
            UnityEngine.Events.UnityAction<bool> action
        )
        {
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.onValueChanged.AddListener(action);
        }

        public static void Set_DropDown_Event(
            Dropdown dropdown,
            UnityEngine.Events.UnityAction<int> action
        )
        {
            dropdown.onValueChanged = new Dropdown.DropdownEvent();
            dropdown.onValueChanged.AddListener(action);
        }

        public static readonly System.Action ToggleVisibility_OnClick_Action = new System.Action(
            ToggleVisibility_Click
        );

        public static void ToggleVisibility_Click()
        {
            DamageMeter_obj.active = !DamageMeter_obj.active;
        }

        public static readonly System.Action OnOff_OnClick_Action = new System.Action(OnOff_Click);

        public static void OnOff_Click()
        {
            On = !On;
        }

        public static readonly System.Action Reset_OnClick_Action = new System.Action(Reset_Click);

        public static void Reset_Click()
        {
            UI.Reset();
        }

        public static readonly System.Action Settings_OnClick_Action = new System.Action(
            Settings_Click
        );

        public static void Settings_Click()
        {
            Settings_panel.active = !Settings_panel.active;
            DamageMeter_content.active = !Settings_panel.active;
        }

        public static readonly System.Action OpenDetails_OnClick_Action = new System.Action(
            OpenDetails
        );

        public static void OpenDetails()
        {
            UI.OpenDetails();
        }

        public static readonly System.Action CloseDetails_OnClick_Action = new System.Action(
            CloseDetails
        );

        public static void CloseDetails()
        {
            UI.CloseDetails();
        }

        public static readonly System.Action<int> DamageType_Dropdown_Action =
            new System.Action<int>(Set_DamageType);

        private static void Set_DamageType(int value)
        {
            if (
                (!Save_Manager.instance.IsNullOrDestroyed())
                && (!DamageType_Dropdown.IsNullOrDestroyed())
            )
            {
                Save_Manager.instance.data.DamageMeter.DamageType = DamageType_Dropdown.value;
            }
        }

        public static readonly System.Action<bool> SeparateDot_Toggle_Action =
            new System.Action<bool>(Set_SeparateDot_Enable);

        private static void Set_SeparateDot_Enable(bool enable)
        {
            if (
                (!Save_Manager.instance.IsNullOrDestroyed())
                && (!SeparateDot_Toggle.IsNullOrDestroyed())
            )
            {
                Save_Manager.instance.data.DamageMeter.SeparateHitAndDot = SeparateDot_Toggle.isOn;
            }
        }

        //Select Top Skill
        [HarmonyPatch(typeof(Button), "Press")]
        public class Button_Press
        {
            [HarmonyPostfix]
            static void Postfix(ref Button __instance)
            {
                try
                {
                    if (Details_panel.active)
                    {
                        if (__instance.name.Contains(Details_TopSkillsName))
                        {
                            int i = System.Convert.ToInt32(__instance.name.Split('_')[1]);
                            UI.ShowDetails_Bottom(i);
                        }
                    }
                }
                catch { }
            }
        }
    }
}
