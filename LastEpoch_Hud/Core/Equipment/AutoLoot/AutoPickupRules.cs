namespace LastEpoch_Hud.Scripts.Core.Items.AutoLoot;

/// <summary>Decides direct pickups of non-item drops.</summary>
public static class AutoPickupRules
{
    public static bool PotionHasRoom(int currentCharges, int maxCharges)
    {
        return currentCharges < maxCharges;
    }
}
