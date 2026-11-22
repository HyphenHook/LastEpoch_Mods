using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.AI;

namespace LastEpoch_Hud.Scripts.Mods.Summon;

[RegisterTypeInIl2Cpp]
public class Summon_Options : MonoBehaviour
{
    private readonly Dictionary<BaseHealth, bool> _health = new Dictionary<BaseHealth, bool>();
    private readonly Dictionary<NavMeshAgent, float> _radii = new Dictionary<NavMeshAgent, float>();

    public Summon_Options(IntPtr ptr)
        : base(ptr) { }

    private void RestoreHealth()
    {
        foreach (KeyValuePair<BaseHealth, bool> entry in _health)
        {
            if (!entry.Key.IsNullOrDestroyed())
            {
                entry.Key.damageable = entry.Value;
            }
        }
        _health.Clear();
    }

    private void RestoreRadii()
    {
        foreach (KeyValuePair<NavMeshAgent, float> entry in _radii)
        {
            if (!entry.Key.IsNullOrDestroyed())
            {
                entry.Key.radius = entry.Value;
            }
        }
        _radii.Clear();
    }

    private void OnDisable()
    {
        RestoreHealth();
        RestoreRadii();
    }

    private void Update()
    {
        bool ready =
            Scenes.IsGameScene()
            && !Save_Manager.instance.IsNullOrDestroyed()
            && Save_Manager.instance.initialized
            && !Refs_Manager.player_actor.IsNullOrDestroyed();
        if (!ready)
        {
            OnDisable();
            return;
        }
        Save_Manager.Data.Summon options = Save_Manager.instance.data.Summon;
        if (!options.Enable_GodMode)
        {
            RestoreHealth();
        }
        if (!options.Enable_DontCollide)
        {
            RestoreRadii();
        }
        if (!options.Enable_GodMode && !options.Enable_DontCollide && !options.Enable_Forever)
        {
            return;
        }
        SummonTracker tracker;
        if (
            !Refs_Manager.player_actor.TryGetExistingSummonTracker(out tracker)
            || tracker.IsNullOrDestroyed()
        )
        {
            return;
        }
        if (tracker.summons == null)
        {
            return;
        }
        foreach (Summoned summoned in tracker.summons)
        {
            if (summoned.IsNullOrDestroyed() || summoned.gameObject.IsNullOrDestroyed())
            {
                continue;
            }
            if (options.Enable_GodMode)
            {
                UnitHealth unit = summoned.gameObject.GetComponent<UnitHealth>();
                BaseHealth target = unit.IsNullOrDestroyed() ? null : unit.TryCast<BaseHealth>();
                if (!target.IsNullOrDestroyed())
                {
                    if (!_health.ContainsKey(target))
                    {
                        _health.Add(target, target.damageable);
                    }
                    target.damageable = false;
                }
            }
            if (options.Enable_DontCollide)
            {
                NavMeshAgent agent = summoned.gameObject.GetComponent<NavMeshAgent>();
                if (!agent.IsNullOrDestroyed())
                {
                    if (!_radii.ContainsKey(agent))
                    {
                        _radii.Add(agent, agent.radius);
                    }
                    agent.radius = 0f;
                }
            }
            if (options.Enable_Forever)
            {
                UnsummonAfterDelay delay = summoned.gameObject.GetComponent<UnsummonAfterDelay>();
                // Match the original Forever option: remove the lifetime component.
                // Existing summons stay permanent after this option is switched off.
                if (!delay.IsNullOrDestroyed())
                {
                    UnityEngine.Object.Destroy(delay);
                }
            }
        }
    }
}
