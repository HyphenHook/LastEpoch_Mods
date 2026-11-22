using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using MelonLoader;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Character;

[RegisterTypeInIl2Cpp]
public class Character_Bank_Anywhere : MonoBehaviour
{
    public static Character_Bank_Anywhere instance { get; private set; }

    public Character_Bank_Anywhere(System.IntPtr ptr)
        : base(ptr) { }

    void Awake()
    {
        instance = this;
    }

    void Update()
    {
        if (
            (Scenes.IsGameScene())
            && (!Refs_Manager.game_uibase.IsNullOrDestroyed())
            && (!Save_Manager.instance.IsNullOrDestroyed())
        )
        {
            if (
                Input.GetKeyDown(Save_Manager.instance.data.KeyBinds.BankStashs)
                && Save_Manager.instance.data.KeyBinds.BankStashs != HudMenuToggle.MenuKey
            )
            {
                if (IsOpen())
                {
                    Refs_Manager.game_uibase.closeStash(false);
                }
                else
                {
                    Refs_Manager.game_uibase.openStash(true, false);
                }
            }
        }
    }

    bool IsOpen()
    {
        bool result = false;
        if (!Refs_Manager.game_uibase.IsNullOrDestroyed())
        {
            result = Refs_Manager.game_uibase.IsStashOpen();
        }

        return result;
    }
}
