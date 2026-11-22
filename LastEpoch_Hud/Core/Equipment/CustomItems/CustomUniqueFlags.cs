namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>Drop and legendary choices of one custom unique.</summary>
public readonly record struct CustomUniqueFlags(
    bool WeaversWill,
    bool CanDropRandomly,
    bool BaseCannotDrop
)
{
    /// <summary>For items without settings: Legendary Potential, drops randomly, base cannot drop.</summary>
    public static readonly CustomUniqueFlags NoSettings = new(false, true, true);
}
