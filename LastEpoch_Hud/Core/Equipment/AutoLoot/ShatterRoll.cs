namespace LastEpoch_Hud.Scripts.Core.Items.AutoLoot;

/// <summary>Chance rules for auto-shatter: whether it triggers and how many shards an affix gives.</summary>
public static class ShatterRoll
{
    public static bool Triggers(int chance, IShatterRandom random)
    {
        if (chance <= 0)
        {
            return false;
        }
        if (chance >= 100)
        {
            return true;
        }
        return random.RollPercent() < chance;
    }

    public static int ShardsFor(
        int affixId,
        int affixChance,
        int quantityChance,
        IShatterRandom random
    )
    {
        if (affixId == 0)
        {
            return 0;
        }
        if (!Triggers(affixChance, random))
        {
            return 0;
        }
        if (quantityChance <= 0)
        {
            return 1;
        }
        if (quantityChance >= 100)
        {
            return 2;
        }
        return random.RollPercent() < quantityChance ? 2 : 1;
    }
}
