using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.ForceDrop;

/// <summary>Shows the localized load-failure label on the Force Drop page.</summary>
internal static class ForceDropLoadError
{
    private const string Caption = "Force Drop failed to load. See the log.";

    /// <summary>Adds the centered error label under content.</summary>
    public static void Show(GameObject content)
    {
        if (content.IsNullOrDestroyed())
        {
            return;
        }
        Font font = HudElements.FindFont(Hud_Manager.hud_object);
        if (font.IsNullOrDestroyed())
        {
            ErrorLog.Report("No font for the load error label", "ForceDropLoadError.Show");
            return;
        }
        Text label = HudElements.Text(
            content,
            "ForceDropLoadError",
            Caption,
            font,
            HudTheme.BodyFontSize,
            HorizontalWrapMode.Wrap
        );
        HudElements.Stretch(label.rectTransform);
        label.alignment = TextAnchor.MiddleCenter;
        HudStyler.ApplyFontScale(label.gameObject);
    }
}
