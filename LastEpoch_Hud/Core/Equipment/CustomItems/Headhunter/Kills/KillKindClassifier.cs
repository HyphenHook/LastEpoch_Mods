namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Kills;

/// <summary>Turns the monster rarity flags into a kill kind. Boss beats miniboss, rare, then magic.</summary>
public static class KillKindClassifier
{
    public static KillKind Classify(bool isBoss, bool isMiniboss, bool isRare, bool isMagic)
    {
        if (isBoss)
        {
            return KillKind.Boss;
        }

        if (isMiniboss)
        {
            return KillKind.Miniboss;
        }

        if (isRare)
        {
            return KillKind.Rare;
        }

        return isMagic ? KillKind.Magic : KillKind.Normal;
    }
}
