using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Kills;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Kills;

/// <summary>Classifies an actor as boss or miniboss.</summary>
internal static class HeadhunterBossKind
{
    /// <summary>Boss/miniboss kind of the actor; false for others or missing.</summary>
    public static bool TryGet(Actor actor, out KillKind kind)
    {
        kind = KillKind.Normal;
        if (actor.IsNullOrDestroyed())
        {
            return false;
        }

        kind = KillKindClassifier.Classify(actor.isBoss(), actor.isMiniboss(), false, false);
        return kind != KillKind.Normal;
    }
}
