using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;
using LastEpoch_Hud.Scripts.ModUI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

/// <summary>Applies the hidden area buff that matches the model scale to the player.</summary>
internal static class HeadhunterAreaBuff
{
    private static readonly HeadhunterAreaMatch _match = new((int)SP.IncreasedAreaForAreaSkills);

    public static void Sync(int liveBuffs)
    {
        StatBuffs buffs = HeadhunterBuffSink.PlayerBuffs();
        if (buffs == null)
        {
            return;
        }

        float factor = HeadhunterConfigLoader.Current.ModelSize.Factor(liveBuffs);
        bool live = HeadhunterBuffSink.IsLive(buffs, HeadhunterAreaMatch.BuffName);
        if (!_match.TryNext(factor, live, out BuffAction action))
        {
            return;
        }

        HeadhunterBuffSink.Apply(buffs, action);
        Log(action);
    }

    public static void Remove()
    {
        BuffAction action = _match.Clear();
        StatBuffs buffs = HeadhunterBuffSink.PlayerBuffs();
        if (buffs == null)
        {
            return;
        }

        HeadhunterBuffSink.Apply(buffs, action);
    }

    private static void Log(BuffAction action)
    {
        if (!ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        Main.logger_instance?.Msg("Headhunter area: " + action.Kind + " +" + action.More);
    }
}
