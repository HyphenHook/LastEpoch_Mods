namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

/// <summary>Picks the visible player model: the form while transformed, else the human model.</summary>
public static class HeadhunterModelSelector
{
    public static HeadhunterModelSource Pick(bool transformed, bool formAlive)
    {
        return transformed && formAlive ? HeadhunterModelSource.Form : HeadhunterModelSource.Base;
    }
}
