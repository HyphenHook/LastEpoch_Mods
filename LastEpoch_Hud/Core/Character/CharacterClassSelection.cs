using System.Collections.Generic;
using System.Linq;

namespace LastEpoch_Hud.Scripts.Core.Character;

/// <summary>Maps the Class dropdown options to class ids.</summary>
public static class CharacterClassSelection
{
    /// <summary>Class order of the options (ascending class id).</summary>
    private static readonly CharacterClassKind[] _order =
    {
        CharacterClassKind.Primalist,
        CharacterClassKind.Mage,
        CharacterClassKind.Sentinel,
        CharacterClassKind.Acolyte,
        CharacterClassKind.Rogue,
    };

    /// <summary>Dropdown option texts (class names), built once.</summary>
    public static IReadOnlyList<string> OptionLabels { get; } =
        _order.Select(kind => kind.ToString()).ToArray();

    /// <summary>Class id of an option; false when the index is out of range.</summary>
    public static bool TryClassIdAt(int optionIndex, out int classId)
    {
        classId = 0;
        if (optionIndex < 0 || optionIndex >= _order.Length)
        {
            return false;
        }

        classId = (int)_order[optionIndex];
        return true;
    }

    /// <summary>First non-blank of the game's localized name, the game's class name, then the enum name.</summary>
    public static string LabelAt(int optionIndex, string localizedName, string className)
    {
        if (optionIndex < 0 || optionIndex >= OptionLabels.Count)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(localizedName))
        {
            return localizedName;
        }

        return string.IsNullOrWhiteSpace(className) ? OptionLabels[optionIndex] : className;
    }

    /// <summary>True when the options are stale: the language changed or the game names became available.</summary>
    public static bool ShouldRebuildOptions(
        bool localeChanged,
        bool builtFromGame,
        bool gameListLoaded
    ) => localeChanged || (gameListLoaded && !builtFromGame);

    /// <summary>Option showing a class id; 0 when the id is unknown.</summary>
    public static int OptionIndexOf(int classId)
    {
        int index = System.Array.IndexOf(_order, (CharacterClassKind)classId);
        return index < 0 ? 0 : index;
    }
}
