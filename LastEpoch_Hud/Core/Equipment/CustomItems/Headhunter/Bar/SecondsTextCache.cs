namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;

/// <summary>Cached digit strings for the countdown, so a per-second refresh does not allocate.</summary>
public sealed class SecondsTextCache
{
    private string[] _texts = System.Array.Empty<string>();

    public string Get(int seconds)
    {
        if (seconds >= _texts.Length)
        {
            Grow(seconds + 1);
        }

        return _texts[seconds];
    }

    private void Grow(int length)
    {
        string[] grown = new string[length];
        for (int i = 0; i < length; i++)
        {
            grown[i] = i < _texts.Length ? _texts[i] : i.ToString();
        }

        _texts = grown;
    }
}
