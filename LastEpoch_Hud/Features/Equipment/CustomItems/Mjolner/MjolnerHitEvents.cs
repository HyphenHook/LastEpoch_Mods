using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mjolner;
using MelonLoader;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mjolner;

/// <summary>Keeps one Mjolner hit hook on the current player and triggers on hit.</summary>
internal static class MjolnerHitEvents
{
    private static readonly AbilityEventListener.OnHitAction _onHit = new System.Action<
        Ability,
        Actor
    >(OnHit);
    private static readonly MjolnerHookWatch _watch = new();
    private static AbilityEventListener _listener;

    internal static void MarkSceneLoaded()
    {
        _watch.MarkSceneLoaded();
    }

    internal static void Update()
    {
        bool enabled = IsEnabled();
        long playerId = PlayerId();
        if (_watch.ShouldUnhook(enabled, playerId))
        {
            Unhook();
            _watch.MarkUnhooked();
            return;
        }

        if (!_watch.ShouldHook(enabled, playerId, !_listener.IsNullOrDestroyed()))
        {
            return;
        }

        if (!Hook())
        {
            return;
        }

        MjolnerTrigger.ResetRun();
        _watch.MarkHooked(playerId);
    }

    private static bool Hook()
    {
        Unhook();
        if (Refs_Manager.player_actor.gameObject.IsNullOrDestroyed())
        {
            return false;
        }

        AbilityEventListener listener =
            Refs_Manager.player_actor.gameObject.GetComponent<AbilityEventListener>();
        if (listener.IsNullOrDestroyed())
        {
            return false;
        }

        listener.remove_onHitEvent(_onHit);
        listener.add_onHitEvent(_onHit);
        _listener = listener;
        return true;
    }

    private static void Unhook()
    {
        if (!_listener.IsNullOrDestroyed())
        {
            _listener.remove_onHitEvent(_onHit);
        }

        _listener = null;
    }

    private static bool IsEnabled()
    {
        return !Save_Manager.instance.IsNullOrDestroyed()
            && Save_Manager.instance.initialized
            && Save_Manager.instance.data.Items.Mjolner.enable;
    }

    private static long PlayerId()
    {
        if (Refs_Manager.player_actor.IsNullOrDestroyed())
        {
            return 0;
        }

        return Refs_Manager.player_actor.Pointer.ToInt64();
    }

    private static void OnHit(Ability ability, Actor hitActor)
    {
        if (Refs_Manager.player_actor.IsNullOrDestroyed())
        {
            return;
        }

        if (!IsMjolnerActive())
        {
            return;
        }

        if (
            Save_Manager.instance.data.Items.Mjolner.ProcAnyLightningSpell
            && !ability.tags.HasFlag(AT.Spell)
        )
        {
            MjolnerTrigger.AllSkills(hitActor);
            return;
        }

        MjolnerTrigger.SocketedSkills(hitActor);
    }

    private static bool IsMjolnerActive()
    {
        Save_Manager.Data.Mjolner settings = Save_Manager.instance.data.Items.Mjolner;
        return settings.enable
            && Refs_Manager.player_actor.itemContainersManager.hasUniqueEquipped(
                CustomUniqueSpecs.Mjolner.UniqueId
            )
            && Refs_Manager.player_actor.stats.GetAttributeValue(CoreAttribute.Attribute.Strength)
                >= settings.StrRequirement
            && Refs_Manager.player_actor.stats.GetAttributeValue(
                CoreAttribute.Attribute.Intelligence
            ) >= settings.IntRequirement;
    }
}
