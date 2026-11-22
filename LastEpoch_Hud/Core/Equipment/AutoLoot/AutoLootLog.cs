using System.Globalization;

namespace LastEpoch_Hud.Scripts.Core.Items.AutoLoot;

/// <summary>Debug lines for auto-loot decisions.</summary>
public static class AutoLootLog
{
    public static string Direct(
        int itemType,
        LootKind kind,
        DropOrigin origin,
        AutoLootAction action
    )
    {
        return "AutoLoot: type="
            + itemType.ToString(CultureInfo.InvariantCulture)
            + " kind="
            + kind.ToString()
            + " action="
            + ActionText(action)
            + OriginText(origin);
    }

    public static string Filter(
        int itemType,
        LootKind kind,
        DropOrigin origin,
        LootVisibility visibility,
        AutoLootAction action
    )
    {
        return "AutoLoot: type="
            + itemType.ToString(CultureInfo.InvariantCulture)
            + " kind="
            + kind.ToString()
            + " filter="
            + VisibilityText(visibility)
            + " action="
            + ActionText(action)
            + OriginText(origin);
    }

    private static string ActionText(AutoLootAction action)
    {
        return action switch
        {
            AutoLootAction.Kept => "kept",
            AutoLootAction.Picked => "picked",
            AutoLootAction.Stored => "stored",
            AutoLootAction.Shattered => "shattered",
            AutoLootAction.Sold => "sold",
            _ => "unknown",
        };
    }

    private static string VisibilityText(LootVisibility visibility)
    {
        return visibility switch
        {
            LootVisibility.Unfiltered => "none",
            LootVisibility.Shown => "shown",
            LootVisibility.Hidden => "hidden",
            _ => "unknown",
        };
    }

    private static string OriginText(DropOrigin origin)
    {
        return origin == DropOrigin.Mod ? " origin=mod" : "";
    }
}
