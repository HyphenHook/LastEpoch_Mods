namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;

/// <summary>Says when the tooltip must be redrawn.</summary>
public sealed class HeadhunterHoverTracker
{
    private int _row = -1;
    private int _stacks;
    private int _layoutVersion;

    public bool Changed(int row, int stacks, int layoutVersion)
    {
        if (row == _row && stacks == _stacks && layoutVersion == _layoutVersion)
        {
            return false;
        }

        _row = row;
        _stacks = stacks;
        _layoutVersion = layoutVersion;
        return true;
    }
}
