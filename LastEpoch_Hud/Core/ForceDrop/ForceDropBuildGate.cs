namespace LastEpoch_Hud.Scripts.Core.ForceDrop;

/// <summary>Lets the Force Drop view build until it is ready; after one failed build it never builds again.</summary>
public sealed class ForceDropBuildGate
{
    private bool _failed;

    /// <summary>True while the view is not ready and no build has failed.</summary>
    public bool ShouldBuild(bool isReady)
    {
        return !isReady && !_failed;
    }

    /// <summary>Records a failed build; later <see cref="ShouldBuild"/> calls return false.</summary>
    public void MarkFailed()
    {
        _failed = true;
    }
}
