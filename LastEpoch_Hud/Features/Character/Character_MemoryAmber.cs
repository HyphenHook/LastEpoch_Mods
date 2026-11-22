using System;
using Il2CppLE.Factions;

namespace LastEpoch_Hud.Scripts.Mods.Character;

internal static class Character_MemoryAmber
{
    const int Amount = 10000;

    public static void Add10000()
    {
        if (!Scenes.IsGameScene() || Refs_Manager.player_actor.IsNullOrDestroyed())
            return;
        try
        {
            var tracker = Refs_Manager.player_actor.gameObject.GetComponent<FactionTracker>();
            if (
                tracker.IsNullOrDestroyed()
                || tracker.factions == null
                || !tracker.factions.TryGetValue(FactionID.TheWeaver, out Faction faction)
                || faction.IsNullOrDestroyed()
            )
            {
                Main.logger_instance?.Warning("Memory Amber: Weaver faction unavailable.");
                return;
            }
            if (!faction.IsMember)
            {
                Main.logger_instance?.Warning(
                    "Memory Amber: join the Woven faction before adding amber."
                );
                return;
            }
            int amount = (int)
                Math.Min(Amount, Math.Max(0L, (long)Faction.MaxFavor - faction.Favor));
            if (amount == 0)
            {
                Main.logger_instance?.Warning(
                    "Memory Amber: currency balance is already at its limit."
                );
                return;
            }
            // Memory Amber is Weaver Favor. Update native currency/events without
            // multiplying this grant or awarding faction reputation.
            using (Mods.UI.SessionGainCounters.SuppressManualGrants())
                faction.GainFavor(amount, true, true);
            faction.SaveAndSync(false);
            Main.logger_instance?.Msg("Memory Amber: added " + amount + ".");
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Error("Add Memory Amber failed: " + ex.Message);
        }
    }
}
