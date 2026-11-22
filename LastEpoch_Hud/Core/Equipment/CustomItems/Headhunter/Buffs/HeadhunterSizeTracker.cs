using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

/// <summary>Remembers the model and factor last applied and decides the next step.</summary>
public sealed class HeadhunterSizeTracker
{
    public const int NoModel = 0;

    private int _modelId = NoModel;
    private float _factor = HeadhunterSizeCurve.NormalFactor;

    public HeadhunterSizeAction Next(int modelId, float factor, bool scaleIntact)
    {
        if (modelId == _modelId && factor == _factor && scaleIntact)
        {
            return HeadhunterSizeAction.None;
        }

        bool restore = ShouldRestore(scaleIntact);
        _modelId = modelId;
        _factor = factor;
        return restore ? HeadhunterSizeAction.RestoreThenRescale : HeadhunterSizeAction.Rescale;
    }

    public bool ShouldRestore(bool scaleIntact)
    {
        return scaleIntact && _modelId != NoModel && _factor != HeadhunterSizeCurve.NormalFactor;
    }

    public void Reset()
    {
        _modelId = NoModel;
        _factor = HeadhunterSizeCurve.NormalFactor;
    }
}
