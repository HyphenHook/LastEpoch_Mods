using System;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter;

/// <summary>Game names for stat ids.</summary>
internal static class HeadhunterStatNames
{
    public static readonly Func<int, string> EnumName = id => ((SP)id).ToString();
    public static readonly Func<int, string> TagEnumName = id => ((AT)id).ToString();

    public static string GameTagName(int tags)
    {
        if (tags == 0)
        {
            return null;
        }

        try
        {
            return Tags.getTagStringForPropertyTag((AT)tags);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "HeadhunterStatNames.GameTagName");
            return null;
        }
    }

    public static bool TryRead(int statId, out string name, out bool addedAsPercent)
    {
        name = null;
        addedAsPercent = false;
        try
        {
            BasePropertyInfo info = PropertyList.get().GetPropertyInfo((SP)statId, AT.None, 0);
            if (info == null)
            {
                return false;
            }

            name = info.getPropertyName(0);
            addedAsPercent = info.displayAddedAsPercentage;
            return !string.IsNullOrEmpty(name);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "HeadhunterStatNames.TryRead");
            return false;
        }
    }
}
