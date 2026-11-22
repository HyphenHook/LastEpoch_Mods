using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mjolner;
using MelonLoader;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mjolner;

/// <summary>Casts Mjolner's triggered skills and holds their run state.</summary>
internal static class MjolnerTrigger
{
    internal static Ability[] Abilities;
    private static System.DateTime[] _times;
    private static bool _trigger;

    internal static void AllSkills(Actor hitActor)
    {
        if (hitActor.IsNullOrDestroyed() || _trigger)
        {
            return;
        }

        _trigger = true;
        if (ShouldProc())
        {
            CastLightningSpells(hitActor);
        }

        _trigger = false;
    }

    /// <summary>Drops run state: re-reads socketed skills, restarts their cooldowns, clears the re-entry flag.</summary>
    internal static void ResetRun()
    {
        _trigger = false;
        Abilities = new Ability[3];
        _times = new System.DateTime[3];
        if (Refs_Manager.ability_manager.IsNullOrDestroyed())
        {
            return;
        }

        FindSocketedAbilities();
    }

    internal static void SocketedSkills(Actor hitActor)
    {
        if (hitActor.IsNullOrDestroyed() || _trigger)
        {
            return;
        }

        _trigger = true;
        for (int i = 0; i < Abilities.Length; i++)
        {
            if (Abilities[i].IsNullOrDestroyed() || i >= _times.Length)
            {
                continue;
            }

            double cooldown = Save_Manager.instance.data.Items.Mjolner.SocketedCooldown;
            if (cooldown < 250)
            {
                cooldown = 250;
            }

            if ((System.DateTime.Now - _times[i]).TotalMilliseconds <= cooldown)
            {
                continue;
            }

            CastFree(Abilities[i], hitActor);
            _times[i] = System.DateTime.Now;
        }

        _trigger = false;
    }

    private static bool ShouldProc()
    {
        if (Refs_Manager.player_treedata.IsNullOrDestroyed())
        {
            return false;
        }

        Save_Manager.Data.Mjolner mjolner = Save_Manager.instance.data.Items.Mjolner;
        return MjolnerTriggerChance.Procs(
            mjolner.MinTriggerChance,
            mjolner.MaxTriggerChance,
            Random.value,
            Random.value
        );
    }

    private static void CastLightningSpells(Actor hitActor)
    {
        foreach (Ability ability in Refs_Manager.player_actor.GetAbilityList().abilities)
        {
            if (!ability.tags.HasFlag(AT.Lightning) || !ability.tags.HasFlag(AT.Spell))
            {
                continue;
            }

            CastFree(ability, hitActor);
        }
    }

    private static void FindSocketedAbilities()
    {
        int i = 0;
        foreach (Ability ability in Refs_Manager.ability_manager.abilities)
        {
            if (ability.IsNullOrDestroyed() || i >= 3 || !IsSocketedSkill(ability.abilityName))
            {
                continue;
            }

            Abilities[i] = ability;
            _times[i] = System.DateTime.Now;
            i++;
        }
    }

    private static bool IsSocketedSkill(string abilityName)
    {
        Save_Manager.Data.Mjolner mjolner = Save_Manager.instance.data.Items.Mjolner;
        return abilityName == mjolner.SockectedSkill_0
            || abilityName == mjolner.SockectedSkill_1
            || abilityName == mjolner.SockectedSkill_2;
    }

    private static void CastFree(Ability ability, Actor hitActor)
    {
        float backupManaCost = ability.manaCost;
        ability.manaCost = 0;
        // AbilityMutator is needed here for the addedManaCost variable.
        ability.castAtTargetFromConstructorAfterDelay(
            Refs_Manager.player_actor.abilityObjectConstructor,
            Vector3.zero,
            hitActor.position(),
            0,
            UseType.Indirect
        );
        ability.manaCost = backupManaCost;
    }
}
