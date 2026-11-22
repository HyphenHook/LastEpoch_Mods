namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter;

/// <summary>Lets each newly active scene through once, keyed by Unity's scene handle.</summary>
public sealed class HeadhunterActiveSceneGate
{
    private int _handle;

    /// <summary>True when the handle is a valid scene other than the last entered one; remembers nothing.</summary>
    public bool IsNew(int handle)
    {
        return handle != 0 && handle != _handle;
    }

    public bool TryEnter(int handle, string sceneName)
    {
        if (!IsNew(handle))
        {
            return false;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            return false;
        }

        _handle = handle;
        return true;
    }
}
