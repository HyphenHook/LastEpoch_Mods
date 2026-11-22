using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Resolve;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

/// <summary>Clears Headhunter run state: mechanic state, player buffs, bar.</summary>
internal static class HeadhunterBuffClearer
{
    private static readonly HeadhunterClearRule _clear = new();

    public static void ClearAll(HeadhunterResolvedConfig config)
    {
        RemoveBuffs(config);
        HeadhunterTimerPause.Clear();
        HeadhunterConfigLoader.Mechanic?.Reset();
        HeadhunterBuffBar.MarkDirty();
        HeadhunterModelScaler.Restore();
        HeadhunterAreaBuff.Remove();
        HeadhunterReach.Reset();
        HeadhunterDash.Reset();
        HeadhunterAura.Remove();
    }

    private static void RemoveBuffs(HeadhunterResolvedConfig config)
    {
        StatBuffs buffs = HeadhunterBuffSink.PlayerBuffs();
        if (config == null || buffs == null)
        {
            return;
        }

        HeadhunterBuffSink.Apply(buffs, _clear.RemoveAll(config));
    }
}
