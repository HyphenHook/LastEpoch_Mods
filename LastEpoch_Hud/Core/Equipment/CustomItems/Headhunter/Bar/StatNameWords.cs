using System.Text;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;

/// <summary>Turns an enum name into spaced words.</summary>
public static class StatNameWords
{
    public static string Split(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "";
        }

        StringBuilder text = new(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (StartsWord(name, i))
            {
                text.Append(' ');
            }

            text.Append(name[i]);
        }

        return text.ToString();
    }

    private static bool StartsWord(string name, int index)
    {
        if (index == 0 || !char.IsUpper(name[index]))
        {
            return false;
        }

        if (!char.IsUpper(name[index - 1]))
        {
            return true;
        }

        return index + 1 < name.Length && char.IsLower(name[index + 1]);
    }
}
