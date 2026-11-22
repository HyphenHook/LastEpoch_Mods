namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>A config file as text. Read returns null and Write returns false on failure.</summary>
public interface IConfigText
{
    bool Exists();

    string Read();

    bool Write(string text);
}
