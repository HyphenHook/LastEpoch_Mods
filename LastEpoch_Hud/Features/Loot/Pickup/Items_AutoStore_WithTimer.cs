using MelonLoader;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.Pickup;

[RegisterTypeInIl2Cpp]
public class Items_AutoStore_WithTimer : MonoBehaviour
{
    public static Items_AutoStore_WithTimer instance { get; private set; }

    public Items_AutoStore_WithTimer(System.IntPtr ptr)
        : base(ptr) { }

    public static bool running = false;
    static float nextStoreAt;

    void Awake()
    {
        instance = this;
    }

    void Update()
    {
        // Always service the cheap debounced on-drop queue. The GameObject stays
        // active even when the periodic timer feature itself is disabled.
        Items_AutoStore_OnPickup.FlushPending();

        if (
            (!Scenes.IsGameScene())
            || Save_Manager.instance.IsNullOrDestroyed()
            || Save_Manager.instance.data.IsNullOrDestroyed()
        )
        {
            running = false;
            return;
        }

        if (!Save_Manager.instance.data.Items.Pickup.Enable_AutoStore_Timer)
        {
            running = false;
            return;
        }

        float interval = Mathf.Max(1f, Save_Manager.instance.data.Items.Pickup.AutoStore_Timer);
        float now = Time.realtimeSinceStartup;

        if (!running)
        {
            nextStoreAt = now + interval;
            running = true;
            return;
        }

        if (now >= nextStoreAt)
        {
            Items_AutoStore_OnPickup.StoreNow();
            nextStoreAt = now + interval;
        }
    }
}
