namespace LastEpoch_Hud.Scripts.Core.Items;

/// <summary>Maps loaded drop rarity 5/6 to 4.</summary>
public static class DropRarityClamp
{
    public const byte MaxLoadableRarity = 4;

    public static bool TryClamp(byte rarity, out byte clamped)
    {
        clamped = rarity;
        if (rarity != 5 && rarity != 6)
        {
            return false;
        }

        clamped = MaxLoadableRarity;
        return true;
    }
}
