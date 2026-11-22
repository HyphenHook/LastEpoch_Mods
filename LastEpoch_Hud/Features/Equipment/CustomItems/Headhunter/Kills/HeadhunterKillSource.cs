using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Kills;
using MelonLoader;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Kills;

/// <summary>Hooks the player's kill events and reports Headhunter-relevant kills.</summary>
internal static class HeadhunterKillSource
{
    private static readonly AbilityEventListener.OnKillAction _onKill = new System.Action<
        Ability,
        Actor
    >(OnKill);
    private static readonly SummonTracker.MinionKillAction _onMinionKill = new System.Action<
        Summoned,
        Ability,
        Actor
    >(OnMinionKill);
    private static readonly List<HeadhunterStatKey> _modStats = new();
    private static readonly List<KillMod> _mods = new();
    private static readonly KillDeduper _deduper = new();
    private static readonly IntervalGate _trackerRetry = new(1.0);
    private static System.IntPtr _hookedActor;
    private static AbilityEventListener _listener;
    private static SummonTracker _tracker;

    public static void EnsureHooked()
    {
        if (Refs_Manager.player_actor.IsNullOrDestroyed())
        {
            return;
        }

        if (Refs_Manager.player_actor.Pointer != _hookedActor || _listener.IsNullOrDestroyed())
        {
            Unhook();
            HookListener();
            _hookedActor = Refs_Manager.player_actor.Pointer;
        }

        if (!_tracker.IsNullOrDestroyed() || !_trackerRetry.IsDue(Time.unscaledTime))
        {
            return;
        }

        HookTracker();
    }

    internal static bool IsHeadhunterWorn()
    {
        if (Refs_Manager.player_actor.IsNullOrDestroyed())
        {
            return false;
        }

        return Refs_Manager.player_actor.itemContainersManager.hasUniqueEquipped(
            CustomUniqueSpecs.Headhunter.UniqueId
        );
    }

    private static void Unhook()
    {
        if (!_listener.IsNullOrDestroyed())
        {
            _listener.remove_onKillEvent(_onKill);
        }

        if (!_tracker.IsNullOrDestroyed())
        {
            _tracker.remove_minionKillEvent(_onMinionKill);
        }

        _listener = null;
        _tracker = null;
    }

    private static void HookListener()
    {
        _listener = PlayerComponent<AbilityEventListener>();
        if (_listener.IsNullOrDestroyed())
        {
            return;
        }

        _listener.remove_onKillEvent(_onKill);
        _listener.add_onKillEvent(_onKill);
    }

    private static void HookTracker()
    {
        if (!Refs_Manager.player_actor.TryGetExistingSummonTracker(out SummonTracker tracker))
        {
            return;
        }

        if (tracker.IsNullOrDestroyed())
        {
            return;
        }

        tracker.remove_minionKillEvent(_onMinionKill);
        tracker.add_minionKillEvent(_onMinionKill);
        _tracker = tracker;
    }

    private static T PlayerComponent<T>()
        where T : UnityEngine.Component
    {
        if (Refs_Manager.player_actor.IsNullOrDestroyed())
        {
            return null;
        }

        if (Refs_Manager.player_actor.gameObject.IsNullOrDestroyed())
        {
            return null;
        }

        return Refs_Manager.player_actor.gameObject.GetComponent<T>();
    }

    private static void OnKill(Ability ability, Actor killedActor)
    {
        Report(killedActor, false);
    }

    private static void OnMinionKill(Summoned summon, Ability ability, Actor killedActor)
    {
        Report(killedActor, true);
    }

    private static void Report(Actor killed, bool byMinion)
    {
        if (killed.IsNullOrDestroyed())
        {
            return;
        }

        KillKind kind = KillKindClassifier.Classify(
            killed.isBoss(),
            killed.isMiniboss(),
            killed.IsRare,
            killed.rarity == Actor.Rarity.Magic
        );
        if (kind == KillKind.Normal)
        {
            return;
        }

        if (!IsHeadhunterWorn())
        {
            return;
        }

        if (!_deduper.TryClaim(Time.frameCount, killed.GetInstanceID()))
        {
            return;
        }

        ReadMods(killed);
        HeadhunterKillHandler.Handle(new KillInfo(kind, byMinion, _modStats, _mods));
    }

    private static void ReadMods(Actor killed)
    {
        _modStats.Clear();
        _mods.Clear();
        var manager = MonsterRarityManager.getInstance();
        if (manager.IsNullOrDestroyed())
        {
            return;
        }

        Il2CppSystem.Collections.Generic.HashSet<int> keys = killed.getAppliedModKeys();
        if (keys == null)
        {
            return;
        }

        foreach (int key in keys)
        {
            AddMod(manager, key);
        }
    }

    private static void AddMod(MonsterRarityManager manager, int key)
    {
        if (!manager.TryGetMonsterModFromKey(key, out MonsterMod mod))
        {
            return;
        }

        if (mod.IsNullOrDestroyed())
        {
            return;
        }

        if (mod.modType != MonsterMod.ModType.Prefix && mod.modType != MonsterMod.ModType.Suffix)
        {
            return;
        }

        int start = _modStats.Count;
        AddStats(mod);
        _mods.Add(new KillMod(mod.modKey, start, _modStats.Count - start));
    }

    private static void AddStats(MonsterMod mod)
    {
        StatsMonsterMod statsMod = mod.TryCast<StatsMonsterMod>();
        if (statsMod.IsNullOrDestroyed() || statsMod.stats == null)
        {
            return;
        }

        foreach (Stats.Stat stat in statsMod.stats)
        {
            _modStats.Add(new HeadhunterStatKey((int)stat.property, (int)stat.tags));
        }
    }
}
