using MelonLoader;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Character;

[RegisterTypeInIl2Cpp]
public class Character_AutoPotions : MonoBehaviour
{
    public static Character_AutoPotions instance { get; private set; }

    public Character_AutoPotions(System.IntPtr ptr)
        : base(ptr) { }

    void Awake()
    {
        instance = this;
    }

    float nextPotionTime = 0f;

    void Update()
    {
        if (
            (Scenes.IsGameScene())
            && (!Refs_Manager.player_health.IsNullOrDestroyed())
            && (!Refs_Manager.health_potion.IsNullOrDestroyed())
            && (!Save_Manager.instance.IsNullOrDestroyed())
        )
        {
            if (Save_Manager.instance.data.Character.Cheats.Enable_AutoPot)
            {
                if (UnityEngine.Time.unscaledTime < nextPotionTime)
                {
                    return;
                }
                int player_health_percent = (int)(
                    Refs_Manager.player_health.currentHealth
                    / Refs_Manager.player_health.maxHealth
                    * 100
                );
                int auto_pot_percent = (int)(
                    Save_Manager.instance.data.Character.Cheats.autoPot / 255 * 100
                );
                if (player_health_percent < auto_pot_percent)
                {
                    nextPotionTime = UnityEngine.Time.unscaledTime + 0.35f;
                    Refs_Manager.health_potion.UsePotion();
                }
            }
        }
    }
}
