namespace LastEpoch_Hud.Scripts.Core.Items.AutoLoot;

/// <summary>Percent dice for auto-shatter rolls.</summary>
public interface IShatterRandom
{
    /// <summary>Returns a number from 0 to 99 inclusive.</summary>
    int RollPercent();
}
