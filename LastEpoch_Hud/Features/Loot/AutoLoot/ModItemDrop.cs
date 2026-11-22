using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Items.AutoLoot;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;

/// <summary>Drops items for the mod (the only mod drop path) so auto-loot never sells or shatters them.</summary>
public static class ModItemDrop
{
    private static readonly DropOriginTracker _origin = new();

    public static DropOrigin CurrentOrigin => _origin.Current;

    public static void Drop(GroundItemManager ground, Actor actor, ItemData item, Vector3 position)
    {
        _origin.RunAsMod(() => ground.dropItemForPlayer(actor, item, position, false));
    }
}
