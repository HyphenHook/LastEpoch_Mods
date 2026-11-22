using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;

/// <summary>Finds a dropped tome interaction by id.</summary>
internal static class TomeLookup
{
    public static int IndexOfId<T>(Il2CppSystem.Collections.Generic.List<T> list, uint id)
        where T : PickupGroundObjectInteraction
    {
        if (list.IsNullOrDestroyed())
        {
            return -1;
        }

        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].id == id)
            {
                return i;
            }
        }

        return -1;
    }
}
