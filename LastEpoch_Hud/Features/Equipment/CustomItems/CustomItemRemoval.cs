using Il2Cpp;
using MelonLoader;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

/// <summary>Tells whether a removed equipment entry is a given custom unique.</summary>
internal static class CustomItemRemoval
{
    public static bool IsUnique(ItemContainerEntryHandler removed, ushort uniqueId)
    {
        if (removed.IsNullOrDestroyed() || removed.entry.IsNullOrDestroyed())
        {
            return false;
        }

        ItemData data = removed.entry.data;
        return !data.IsNullOrDestroyed() && data.uniqueID == uniqueId;
    }
}
