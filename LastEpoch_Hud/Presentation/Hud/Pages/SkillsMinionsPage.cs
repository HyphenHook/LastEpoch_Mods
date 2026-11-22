using System;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.Core.ModUI.Sliders;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>Skills &gt; Minions HUD page. State resets on HUD re-bind (Build).</summary>
internal static class SkillsMinionsPage
{
    private static HudFormPage page;

    public static void Build(HudPageId pageId, GameObject parent, GameObject hud, Font font)
    {
        page = HudFormPage.Build(parent, hud, font, pageId);
        if (page == null)
            return;
        BuildSkeletons();
        BuildWraiths();
        BuildMages();
        BuildBoneGolems();
        BuildVolatileZombies();
        BuildDreadShades();
    }

    public static void Show() => page?.Show();

    public static void Hide() => page?.Hide();

    public static void Refresh() => page?.RefreshValues();

    private static void BuildSkeletons()
    {
        var c = page.AddCard("Skeletons", "Skeletons");
        AddSlider(
            c,
            "SkeletonPassive",
            "Additional Skeletons From Passives",
            SkillSliderLimits.SkeletonFromPassives,
            () =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .Skeletons
                    .Enable_additionalSkeletonsFromPassives,
            v =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .Skeletons
                    .Enable_additionalSkeletonsFromPassives = v,
            () =>
                Save_Manager.instance.data.Skills.Minions.Skeletons.additionalSkeletonsFromPassives,
            v =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .Skeletons
                    .additionalSkeletonsFromPassives = (int)v
        );
        AddSlider(
            c,
            "SkeletonTree",
            "Additional Skeletons From Skill Tree",
            SkillSliderLimits.SkeletonFromSkillTree,
            () =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .Skeletons
                    .Enable_additionalSkeletonsFromSkillTree,
            v =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .Skeletons
                    .Enable_additionalSkeletonsFromSkillTree = v,
            () =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .Skeletons
                    .additionalSkeletonsFromSkillTree,
            v =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .Skeletons
                    .additionalSkeletonsFromSkillTree = (int)v
        );
        AddSlider(
            c,
            "SkeletonPerCast",
            "Additional Skeletons Per Cast",
            SkillSliderLimits.SkeletonPerCast,
            () =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .Skeletons
                    .Enable_additionalSkeletonsPerCast,
            v =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .Skeletons
                    .Enable_additionalSkeletonsPerCast = v,
            () => Save_Manager.instance.data.Skills.Minions.Skeletons.additionalSkeletonsPerCast,
            v =>
                Save_Manager.instance.data.Skills.Minions.Skeletons.additionalSkeletonsPerCast =
                    (int)v
        );
        AddSlider(
            c,
            "SkeletonResummon",
            "Chance To Resummon On Death",
            SkillSliderLimits.SkeletonResummonChance,
            () =>
                Save_Manager.instance.data.Skills.Minions.Skeletons.Enable_chanceToResummonOnDeath,
            v =>
                Save_Manager.instance.data.Skills.Minions.Skeletons.Enable_chanceToResummonOnDeath =
                    v,
            () => Save_Manager.instance.data.Skills.Minions.Skeletons.chanceToResummonOnDeath,
            v => Save_Manager.instance.data.Skills.Minions.Skeletons.chanceToResummonOnDeath = v
        );
        AddToggle(
            c,
            "ForceArcher",
            "Force Archer",
            () => Save_Manager.instance.data.Skills.Minions.Skeletons.Enable_forceArcher,
            v => Save_Manager.instance.data.Skills.Minions.Skeletons.Enable_forceArcher = v
        );
        AddToggle(
            c,
            "ForceBrawler",
            "Force Brawler",
            () => Save_Manager.instance.data.Skills.Minions.Skeletons.Enable_forceBrawler,
            v => Save_Manager.instance.data.Skills.Minions.Skeletons.Enable_forceBrawler = v
        );
        AddToggle(
            c,
            "ForceWarrior",
            "Force Warrior",
            () => Save_Manager.instance.data.Skills.Minions.Skeletons.Enable_forceWarrior,
            v => Save_Manager.instance.data.Skills.Minions.Skeletons.Enable_forceWarrior = v
        );
    }

    private static void BuildWraiths()
    {
        var c = page.AddCard("Wraiths", "Wraiths");
        AddSlider(
            c,
            "WraithMax",
            "Additional Maximum Wraiths",
            SkillSliderLimits.WraithMaximum,
            () => Save_Manager.instance.data.Skills.Minions.Wraiths.Enable_additionalMaxWraiths,
            v => Save_Manager.instance.data.Skills.Minions.Wraiths.Enable_additionalMaxWraiths = v,
            () => Save_Manager.instance.data.Skills.Minions.Wraiths.additionalMaxWraiths,
            v => Save_Manager.instance.data.Skills.Minions.Wraiths.additionalMaxWraiths = (int)v
        );
        AddSlider(
            c,
            "WraithDelay",
            "Delayed Wraiths",
            SkillSliderLimits.WraithDelayed,
            () => Save_Manager.instance.data.Skills.Minions.Wraiths.Enable_delayedWraiths,
            v => Save_Manager.instance.data.Skills.Minions.Wraiths.Enable_delayedWraiths = v,
            () => Save_Manager.instance.data.Skills.Minions.Wraiths.delayedWraiths,
            v => Save_Manager.instance.data.Skills.Minions.Wraiths.delayedWraiths = (int)v
        );
        AddSlider(
            c,
            "WraithCastSpeed",
            "Increased Cast Speed",
            SkillSliderLimits.WraithCastSpeed,
            () => Save_Manager.instance.data.Skills.Minions.Wraiths.Enable_increasedCastSpeed,
            v => Save_Manager.instance.data.Skills.Minions.Wraiths.Enable_increasedCastSpeed = v,
            () => Save_Manager.instance.data.Skills.Minions.Wraiths.increasedCastSpeed,
            v => Save_Manager.instance.data.Skills.Minions.Wraiths.increasedCastSpeed = v
        );
        AddToggle(
            c,
            "WraithNoLimit",
            "Disable Limit To 2 Wraiths",
            () => Save_Manager.instance.data.Skills.Minions.Wraiths.Enable_limitedTo2Wraiths,
            v => Save_Manager.instance.data.Skills.Minions.Wraiths.Enable_limitedTo2Wraiths = v
        );
        AddToggle(
            c,
            "WraithNoDecay",
            "Wraiths Do Not Decay",
            () => Save_Manager.instance.data.Skills.Minions.Wraiths.Enable_wraithsDoNotDecay,
            v => Save_Manager.instance.data.Skills.Minions.Wraiths.Enable_wraithsDoNotDecay = v
        );
    }

    private static void BuildMages()
    {
        var c = page.AddCard("Mages", "Mage");
        AddSlider(
            c,
            "MagePassive",
            "Additional Mages From Passives",
            SkillSliderLimits.MageFromPassives,
            () =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .Mages
                    .Enable_additionalSkeletonsFromPassives,
            v =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .Mages
                    .Enable_additionalSkeletonsFromPassives = v,
            () => Save_Manager.instance.data.Skills.Minions.Mages.additionalSkeletonsFromPassives,
            v =>
                Save_Manager.instance.data.Skills.Minions.Mages.additionalSkeletonsFromPassives =
                    (int)v
        );
        AddSlider(
            c,
            "MageItems",
            "Additional Mages From Items",
            SkillSliderLimits.MageFromItems,
            () =>
                Save_Manager.instance.data.Skills.Minions.Mages.Enable_additionalSkeletonsFromItems,
            v =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .Mages
                    .Enable_additionalSkeletonsFromItems = v,
            () => Save_Manager.instance.data.Skills.Minions.Mages.additionalSkeletonsFromItems,
            v =>
                Save_Manager.instance.data.Skills.Minions.Mages.additionalSkeletonsFromItems =
                    (int)v
        );
        AddSlider(
            c,
            "MageTree",
            "Additional Mages From Skill Tree",
            SkillSliderLimits.MageFromSkillTree,
            () =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .Mages
                    .Enable_additionalSkeletonsFromSkillTree,
            v =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .Mages
                    .Enable_additionalSkeletonsFromSkillTree = v,
            () => Save_Manager.instance.data.Skills.Minions.Mages.additionalSkeletonsFromSkillTree,
            v =>
                Save_Manager.instance.data.Skills.Minions.Mages.additionalSkeletonsFromSkillTree =
                    (int)v
        );
        AddSlider(
            c,
            "MagePerCast",
            "Additional Mages Per Cast",
            SkillSliderLimits.MagePerCast,
            () => Save_Manager.instance.data.Skills.Minions.Mages.Enable_additionalSkeletonsPerCast,
            v =>
                Save_Manager.instance.data.Skills.Minions.Mages.Enable_additionalSkeletonsPerCast =
                    v,
            () => Save_Manager.instance.data.Skills.Minions.Mages.additionalSkeletonsPerCast,
            v => Save_Manager.instance.data.Skills.Minions.Mages.additionalSkeletonsPerCast = (int)v
        );
        AddSlider(
            c,
            "MageProjectile",
            "Chance For Two Extra Projectiles",
            SkillSliderLimits.MageProjectileChance,
            () =>
                Save_Manager.instance.data.Skills.Minions.Mages.Enable_chanceForTwoExtraProjectiles,
            v =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .Mages
                    .Enable_chanceForTwoExtraProjectiles = v,
            () => Save_Manager.instance.data.Skills.Minions.Mages.chanceForTwoExtraProjectiles,
            v => Save_Manager.instance.data.Skills.Minions.Mages.chanceForTwoExtraProjectiles = v
        );
        AddToggle(
            c,
            "Cryomancer",
            "Force Cryomancer",
            () => Save_Manager.instance.data.Skills.Minions.Mages.Enable_forceCryomancer,
            v => Save_Manager.instance.data.Skills.Minions.Mages.Enable_forceCryomancer = v
        );
        AddToggle(
            c,
            "DeathKnight",
            "Force Death Knight",
            () => Save_Manager.instance.data.Skills.Minions.Mages.Enable_forceDeathKnight,
            v => Save_Manager.instance.data.Skills.Minions.Mages.Enable_forceDeathKnight = v
        );
        AddToggle(
            c,
            "Pyromancer",
            "Force Pyromancer",
            () => Save_Manager.instance.data.Skills.Minions.Mages.Enable_forcePyromancer,
            v => Save_Manager.instance.data.Skills.Minions.Mages.Enable_forcePyromancer = v
        );
    }

    private static void BuildBoneGolems()
    {
        var c = page.AddCard("BoneGolem", "Bone Golem");
        AddSlider(
            c,
            "GolemsPerSkeletons",
            "Added Golems Per 4 Skeletons",
            SkillSliderLimits.BoneGolemPerSkeletons,
            () =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .BoneGolems
                    .Enable_addedGolemsPer4Skeletons,
            v =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .BoneGolems
                    .Enable_addedGolemsPer4Skeletons = v,
            () => Save_Manager.instance.data.Skills.Minions.BoneGolems.addedGolemsPer4Skeletons,
            v =>
                Save_Manager.instance.data.Skills.Minions.BoneGolems.addedGolemsPer4Skeletons =
                    (int)v
        );
        AddSlider(
            c,
            "Resurrect",
            "Self Resurrect Chance",
            SkillSliderLimits.BoneGolemResurrectChance,
            () => Save_Manager.instance.data.Skills.Minions.BoneGolems.Enable_selfResurrectChance,
            v =>
                Save_Manager.instance.data.Skills.Minions.BoneGolems.Enable_selfResurrectChance = v,
            () => Save_Manager.instance.data.Skills.Minions.BoneGolems.selfResurrectChance,
            v => Save_Manager.instance.data.Skills.Minions.BoneGolems.selfResurrectChance = v
        );
        AddSlider(
            c,
            "FireAura",
            "Increased Fire Aura Area",
            SkillSliderLimits.BoneGolemFireAura,
            () => Save_Manager.instance.data.Skills.Minions.BoneGolems.Enable_increasedFireAuraArea,
            v =>
                Save_Manager.instance.data.Skills.Minions.BoneGolems.Enable_increasedFireAuraArea =
                    v,
            () => Save_Manager.instance.data.Skills.Minions.BoneGolems.increasedFireAuraArea,
            v => Save_Manager.instance.data.Skills.Minions.BoneGolems.increasedFireAuraArea = v
        );
        AddSlider(
            c,
            "ArmorAura",
            "Undead Armor Aura",
            SkillSliderLimits.BoneGolemArmorAura,
            () => Save_Manager.instance.data.Skills.Minions.BoneGolems.Enable_undeadArmorAura,
            v => Save_Manager.instance.data.Skills.Minions.BoneGolems.Enable_undeadArmorAura = v,
            () => Save_Manager.instance.data.Skills.Minions.BoneGolems.undeadArmorAura,
            v => Save_Manager.instance.data.Skills.Minions.BoneGolems.undeadArmorAura = v
        );
        AddSlider(
            c,
            "MoveSpeedAura",
            "Undead Movement Speed Aura",
            SkillSliderLimits.BoneGolemMoveSpeedAura,
            () => Save_Manager.instance.data.Skills.Minions.BoneGolems.Enable_undeadMovespeedAura,
            v =>
                Save_Manager.instance.data.Skills.Minions.BoneGolems.Enable_undeadMovespeedAura = v,
            () => Save_Manager.instance.data.Skills.Minions.BoneGolems.undeadMovespeedAura,
            v => Save_Manager.instance.data.Skills.Minions.BoneGolems.undeadMovespeedAura = v
        );
        AddSlider(
            c,
            "MoveSpeed",
            "Increased Movement Speed",
            SkillSliderLimits.BoneGolemMoveSpeed,
            () => Save_Manager.instance.data.Skills.Minions.BoneGolems.Enable_increasedMoveSpeed,
            v => Save_Manager.instance.data.Skills.Minions.BoneGolems.Enable_increasedMoveSpeed = v,
            () => Save_Manager.instance.data.Skills.Minions.BoneGolems.increasedMoveSpeed,
            v => Save_Manager.instance.data.Skills.Minions.BoneGolems.increasedMoveSpeed = v
        );
        AddToggle(
            c,
            "Twins",
            "Twins",
            () => Save_Manager.instance.data.Skills.Minions.BoneGolems.Enable_twins,
            v => Save_Manager.instance.data.Skills.Minions.BoneGolems.Enable_twins = v
        );
        AddToggle(
            c,
            "Slam",
            "Has Slam Attack",
            () => Save_Manager.instance.data.Skills.Minions.BoneGolems.Enable_hasSlamAttack,
            v => Save_Manager.instance.data.Skills.Minions.BoneGolems.Enable_hasSlamAttack = v
        );
    }

    private static void BuildVolatileZombies()
    {
        var c = page.AddCard("VolatileZombies", "Volatile Zombies");
        AddSlider(
            c,
            "ZombieMinionDeath",
            "Chance To Cast From Minion Death",
            SkillSliderLimits.ZombieMinionDeathChance,
            () =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .VolatileZombies
                    .Enable_chanceToCastFromMinionDeath,
            v =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .VolatileZombies
                    .Enable_chanceToCastFromMinionDeath = v,
            () =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .VolatileZombies
                    .chanceToCastFromMinionDeath,
            v =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .VolatileZombies
                    .chanceToCastFromMinionDeath = v
        );
        AddSlider(
            c,
            "ZombieInfernalShade",
            "Infernal Shade Chance On Death",
            SkillSliderLimits.ZombieInfernalShadeChance,
            () =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .VolatileZombies
                    .Enable_chanceToCastInfernalShadeOnDeath,
            v =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .VolatileZombies
                    .Enable_chanceToCastInfernalShadeOnDeath = v,
            () =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .VolatileZombies
                    .chanceToCastInfernalShadeOnDeath,
            v =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .VolatileZombies
                    .chanceToCastInfernalShadeOnDeath = v
        );
        AddSlider(
            c,
            "ZombieMarrowShards",
            "Marrow Shards Chance On Death",
            SkillSliderLimits.ZombieMarrowShardsChance,
            () =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .VolatileZombies
                    .Enable_chanceToCastMarrowShardsOnDeath,
            v =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .VolatileZombies
                    .Enable_chanceToCastMarrowShardsOnDeath = v,
            () =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .VolatileZombies
                    .chanceToCastMarrowShardsOnDeath,
            v =>
                Save_Manager
                    .instance
                    .data
                    .Skills
                    .Minions
                    .VolatileZombies
                    .chanceToCastMarrowShardsOnDeath = v
        );
    }

    private static void BuildDreadShades()
    {
        var c = page.AddCard("DreadShades", "Dread Shades");
        AddSlider(
            c,
            "DreadDuration",
            "Duration",
            SkillSliderLimits.DreadShadeDuration,
            () => Save_Manager.instance.data.Skills.Minions.DreadShades.Enable_Duration,
            v => Save_Manager.instance.data.Skills.Minions.DreadShades.Enable_Duration = v,
            () => Save_Manager.instance.data.Skills.Minions.DreadShades.Duration,
            v => Save_Manager.instance.data.Skills.Minions.DreadShades.Duration = v
        );
        AddSlider(
            c,
            "DreadMax",
            "Maximum Dread Shades",
            SkillSliderLimits.DreadShadeMaximum,
            () => Save_Manager.instance.data.Skills.Minions.DreadShades.Enable_Max,
            v => Save_Manager.instance.data.Skills.Minions.DreadShades.Enable_Max = v,
            () => Save_Manager.instance.data.Skills.Minions.DreadShades.max,
            v => Save_Manager.instance.data.Skills.Minions.DreadShades.max = (int)v
        );
        AddSlider(
            c,
            "DreadDecay",
            "Reduced Decay",
            SkillSliderLimits.DreadShadeDecay,
            () => Save_Manager.instance.data.Skills.Minions.DreadShades.Enable_ReduceDecay,
            v => Save_Manager.instance.data.Skills.Minions.DreadShades.Enable_ReduceDecay = v,
            () => Save_Manager.instance.data.Skills.Minions.DreadShades.decay,
            v => Save_Manager.instance.data.Skills.Minions.DreadShades.decay = v
        );
        AddSlider(
            c,
            "DreadRadius",
            "Radius",
            SkillSliderLimits.DreadShadeRadius,
            () => Save_Manager.instance.data.Skills.Minions.DreadShades.Enable_Radius,
            v => Save_Manager.instance.data.Skills.Minions.DreadShades.Enable_Radius = v,
            () => Save_Manager.instance.data.Skills.Minions.DreadShades.radius,
            v => Save_Manager.instance.data.Skills.Minions.DreadShades.radius = v
        );
        AddToggle(
            c,
            "DreadNoLimit",
            "Disable Summon Limit",
            () => Save_Manager.instance.data.Skills.Minions.DreadShades.Enable_DisableLimit,
            v => Save_Manager.instance.data.Skills.Minions.DreadShades.Enable_DisableLimit = v
        );
        AddToggle(
            c,
            "DreadNoDrain",
            "Disable Health Drain",
            () => Save_Manager.instance.data.Skills.Minions.DreadShades.Enable_DisableHealthDrain,
            v => Save_Manager.instance.data.Skills.Minions.DreadShades.Enable_DisableHealthDrain = v
        );
    }

    private static void AddToggle(
        HudFormPage.Card card,
        string id,
        string label,
        Func<bool> read,
        Action<bool> write
    ) =>
        page.AddToggle(
            card,
            id,
            label,
            () => HasSave() && read(),
            v =>
            {
                if (HasSave())
                    write(v);
            }
        );

    private static void AddSlider(
        HudFormPage.Card card,
        string id,
        string label,
        SliderRange range,
        Func<bool> readEnabled,
        Action<bool> writeEnabled,
        Func<float> read,
        Action<float> write
    )
    {
        page.AddToggleSlider(
            card,
            id,
            label,
            string.Empty,
            range.Min,
            range.Max,
            range.WholeNumbers,
            () => HasSave() && readEnabled(),
            v =>
            {
                if (HasSave())
                    writeEnabled(v);
            },
            () => HasSave() ? read() : range.Min,
            v =>
            {
                if (HasSave())
                    write(v);
            }
        );
    }

    private static bool HasSave() =>
        !Save_Manager.instance.IsNullOrDestroyed() && Save_Manager.instance.initialized;
}
