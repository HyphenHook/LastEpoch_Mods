using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

/// <summary>Remembers the renderer set and strength last applied and picks the next aura step.</summary>
public sealed class HeadhunterAuraTracker
{
    private float _strength = HeadhunterAuraCurve.Off;
    private int _key;

    public HeadhunterAuraAction Next(float strength, bool ready, int rendererKey)
    {
        bool on = ready && strength > HeadhunterAuraCurve.Off;
        HeadhunterAuraAction action = Pick(on, strength, rendererKey);
        _strength = on ? strength : HeadhunterAuraCurve.Off;
        _key = on ? rendererKey : 0;
        return action;
    }

    public void Reset()
    {
        _strength = HeadhunterAuraCurve.Off;
        _key = 0;
    }

    private HeadhunterAuraAction Pick(bool on, float strength, int rendererKey)
    {
        bool applied = _strength > HeadhunterAuraCurve.Off;
        if (!on)
        {
            return applied ? HeadhunterAuraAction.Remove : HeadhunterAuraAction.None;
        }
        if (!applied)
        {
            return HeadhunterAuraAction.Apply;
        }
        if (rendererKey != _key)
        {
            return HeadhunterAuraAction.Rebuild;
        }
        return strength == _strength ? HeadhunterAuraAction.None : HeadhunterAuraAction.Retint;
    }
}
