namespace LastEpoch_Hud.Scripts.Core.ModUI;

/// <summary>Reports a key release only when the matching press was seen.</summary>
public sealed class KeyRelease
{
    private bool _pressed;

    /// <summary>True when this frame releases a press seen earlier or in the same frame.</summary>
    public bool Next(bool keyDown, bool keyUp)
    {
        _pressed |= keyDown;
        if (!keyUp || !_pressed)
        {
            return false;
        }

        _pressed = false;
        return true;
    }
}
