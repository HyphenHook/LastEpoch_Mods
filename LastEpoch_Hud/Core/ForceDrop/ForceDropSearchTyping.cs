using System.Text;

namespace LastEpoch_Hud.Scripts.Core.ForceDrop;

/// <summary>
/// Text typed while a search field lacks focus, moved into that field.
/// Once focused, TMP_InputField handles Backspace, Enter, IME and selection.
/// </summary>
public static class ForceDropSearchTyping
{
    /// <summary>
    /// Printable characters of Input.inputString; nothing in a frame with Enter.
    /// Enter ends the edit after the field took the IME's last committed syllable,
    /// and the same syllable still sits in inputString.
    /// </summary>
    public static string Redirected(string input, bool submitKeyPressed)
    {
        if (submitKeyPressed || string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        if (input.IndexOf('\n') >= 0 || input.IndexOf('\r') >= 0)
        {
            return string.Empty;
        }

        var result = new StringBuilder(input.Length);
        foreach (char ch in input)
        {
            if (!char.IsControl(ch))
            {
                result.Append(ch);
            }
        }

        return result.ToString();
    }
}
