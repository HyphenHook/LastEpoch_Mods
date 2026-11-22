using UnityEngine;

namespace LastEpoch_Hud.Scripts;

public partial class Save_Manager
{
    public class Data
    {
        public struct Mods_Structure
        {
            public string ModVersion;
            public KeyBinds KeyBinds;
            public ModsNotInHud modsNotInHud;
            public Character Character;
            public Items Items;
            public Factions Factions;
            public Scenes Scenes;
            public Summon Summon;
            public Skills Skills;
            public DamageMeter DamageMeter;
        }

        public struct Summon
        {
            public bool Enable_GodMode;
            public bool Enable_Forever;
            public bool Enable_DontCollide;
        }

        public struct DamageMeter
        {
            public int DamageType;
            public bool SeparateHitAndDot;
        }

        //KeyBinds
        public struct KeyBinds
        {
            public UnityEngine.KeyCode BankStashs;
            public UnityEngine.KeyCode DamageMeter;
        }

        //Options not in hud (you have to set in defaultconfig before build)
        public struct ModsNotInHud
        {
            public bool Enable_PotionResplenishment;
            public byte Craft_Seal_Tier;
            public bool Craft_No_Forgin_Potencial_Cost;
            public bool Shrines_Unlimited;
            public bool Shrines_Override;
            public int Shrines_Override_id;
        }

        //Character
        public struct Character
        {
            public Cheats Cheats;
            public PermanentBuffs PermanentBuffs;
        }

        public struct Cheats
        {
            public bool Enable_GodMode;
            public bool Enable_LowLife;
            public bool Enable_CanChooseBlessing;
            public bool Enable_UnlockAllIdolsSlots;
            public bool Enable_AutoPot;
            public float autoPot;
            public bool Enable_DensityMultiplier;
            public float DensityMultiplier;
            public bool Enable_ExperienceMultiplier;
            public float ExperienceMultiplier;
            public bool Enable_AbilityMultiplier;
            public float AbilityMultiplier;
            public bool Enable_FavorMultiplier;
            public float FavorMultiplier;
            public bool Enable_ItemDropChance;
            public float ItemDropChance;
            public bool Enable_ItemDropMultiplier;
            public float ItemDropMultiplier;
            public bool Enable_GoldDropChance;
            public float GoldDropChance;
            public bool Enable_GoldDropMultiplier;
            public float GoldDropMultiplier;
            public bool Enable_MemoryAmberMultiplier;
            public uint MemoryAmberMultiplier;
            public bool Enable_WaypointsUnlock;
            public bool Enable_TwoHandedWithShield;
        }

        public struct PermanentBuffs
        {
            public bool Enable_Mod;
            public bool Enable_MoveSpeed_Buff;
            public float MoveSpeed_Buff_Value;
            public bool Enable_CastSpeed_Buff;
            public float CastSpeed_Buff_Value;
            public bool Enable_AttackSpeed_Buff;
            public float AttackSpeed_Buff_Value;
            public bool Enable_Damage_Buff;
            public float Damage_Buff_Value;
            public bool Enable_ManaRegen_Buff;
            public float ManaRegen_Buff_Value;
            public bool Enable_HealthRegen_Buff;
            public float HealthRegen_Buff_Value;
            public bool Enable_CriticalChance_Buff;
            public float CriticalChance_Buff_Value;
            public bool Enable_CriticalMultiplier_Buff;
            public float CriticalMultiplier_Buff_Value;

            public bool Enable_Str_Buff;
            public float Str_Buff_Value;
            public bool Enable_Int_Buff;
            public float Int_Buff_Value;
            public bool Enable_Dex_Buff;
            public float Dex_Buff_Value;
            public bool Enable_Vit_Buff;
            public float Vit_Buff_Value;
            public bool Enable_Att_Buff;
            public float Att_Buff_Value;
            public bool Enable_AoE_Buff;
            public float AoE_Buff_Value;
        }

        //Items
        public struct Items
        {
            public Drop Drop;
            public Pickup Pickup;
            public Req Req;
            public CraftingSlot CraftingSlot;
            public Headhunter Headhunter;
            public Mjolner Mjolner;
        }

        public struct Drop
        {
            public bool HeorotCoreRatesInitialized;
            public bool HeorotCoreOverride;
            public bool HeorotCoreIgnoreScaling;
            public float HeorotCoreSingleChance;
            public float HeorotCoreDoubleChance;
            public float HeorotCoreTripleChance;
            public bool Enable_ForceUnique;
            public bool Enable_ForceSet;
            public bool Enable_ForceLegendary;

            public bool Enable_Implicits;
            public float Implicits_Min;
            public float Implicits_Max;

            public bool Enable_ForginPotencial;
            public float ForginPotencial_Min;
            public float ForginPotencial_Max;

            public bool Enable_ForceSeal;

            public bool Enable_SealTier;
            public float SealTier_Min;
            public float SealTier_Max;

            public bool Enable_SealValue;
            public float SealValue_Min;
            public float SealValue_Max;

            public bool Enable_AffixCount;
            public float AffixCount_Min;
            public float AffixCount_Max;

            // Items with five and six ordinary affixes (never more than three prefixes or three suffixes).
            public bool Enable_ExtraAffixes;
            public float ExtraAffix5Chance; // percent: a four-affix drop gains a fifth
            public float ExtraAffix6Chance; // percent: a five-affix item gains a sixth

            public bool Enable_IdolAffixCount;
            public float IdolAffixCount_Min;
            public float IdolAffixCount_Max;

            public bool Enable_AffixTiers;
            public float AffixTiers_Min;
            public float AffixTiers_Max;

            public bool Enable_AffixValues;
            public float AffixValues_Min;
            public float AffixValues_Max;

            public bool Enable_UniqueMods;
            public float UniqueMods_Min;
            public float UniqueMods_Max;

            public bool Enable_LegendaryPotencial;
            public float LegendaryPotencial_Min;
            public float LegendaryPotencial_Max;

            public bool Enable_WeaverWill;
            public float WeaverWill_Min;
            public float WeaverWill_Max;
        }

        public struct Pickup
        {
            public bool Enable_AutoPickup_Gold;
            public bool Enable_AutoPickup_Keys;
            public bool Enable_AutoPickup_Potions;
            public bool Enable_AutoPickup_XpTome;
            public bool Enable_AutoPickup_FavorTome;
            public bool Enable_AutoPickup_MemoryAmber;
            public bool Enable_AutoPickup_Materials;
            public bool Enable_AutoPickup_WovenEchoes;
            public bool Enable_AutoPickup_FromFilter;

            public bool Enable_AutoStore_OnDrop;
            public bool Enable_AutoStore_OnInventoryOpen;
            public bool Enable_AutoStore_Timer;
            public int AutoStore_Timer;

            public bool Enable_AutoSell_FromFilter;

            public bool Enable_AutoShatter_FromFilter;
            public bool Enable_AutoShatter_UseRune;
            public int AutoShatter_Chance;
            public int AutoShatter_AffixChance;
            public int AutoShatter_QuantityChance;
            public bool AutoShatter_Configured;

            public bool Enable_RangePickup;
            public bool Enable_HideMaterialsNotifications;
        }

        public struct Req
        {
            public bool level;
            public bool classe;
            public bool set;
        }

        public struct CraftingSlot
        {
            public bool Enable_Mod;

            public bool Enable_ForginPotencial;
            public float ForginPotencial;

            public bool Enable_Implicit_0;
            public float Implicit_0;
            public bool Enable_Implicit_1;
            public float Implicit_1;
            public bool Enable_Implicit_2;
            public float Implicit_2;

            public bool Enable_Seal_Tier;
            public int Seal_Tier;
            public bool Enable_Seal_Value;
            public float Seal_Value;

            public bool Enable_Affix_0_Tier;
            public int Affix_0_Tier;
            public bool Enable_Affix_0_Value;
            public float Affix_0_Value;

            public bool Enable_Affix_1_Tier;
            public int Affix_1_Tier;
            public bool Enable_Affix_1_Value;
            public float Affix_1_Value;

            public bool Enable_Affix_2_Tier;
            public int Affix_2_Tier;
            public bool Enable_Affix_2_Value;
            public float Affix_2_Value;

            public bool Enable_Affix_3_Tier;
            public int Affix_3_Tier;
            public bool Enable_Affix_3_Value;
            public float Affix_3_Value;

            public bool Enable_Affix_4_Tier;
            public int Affix_4_Tier;
            public bool Enable_Affix_4_Value;
            public float Affix_4_Value;

            public bool Enable_Affix_5_Tier;
            public int Affix_5_Tier;
            public bool Enable_Affix_5_Value;
            public float Affix_5_Value;

            public bool Enable_Affix_6_Tier;
            public int Affix_6_Tier;
            public bool Enable_Affix_6_Value;
            public float Affix_6_Value;

            public bool Enable_Affix_7_Tier;
            public int Affix_7_Tier;
            public bool Enable_Affix_7_Value;
            public float Affix_7_Value;

            public bool Enable_Affix_8_Tier;
            public int Affix_8_Tier;
            public bool Enable_Affix_8_Value;
            public float Affix_8_Value;

            public bool Enable_UniqueMod_0;
            public float UniqueMod_0;
            public bool Enable_UniqueMod_1;
            public float UniqueMod_1;
            public bool Enable_UniqueMod_2;
            public float UniqueMod_2;
            public bool Enable_UniqueMod_3;
            public float UniqueMod_3;
            public bool Enable_UniqueMod_4;
            public float UniqueMod_4;
            public bool Enable_UniqueMod_5;
            public float UniqueMod_5;
            public bool Enable_UniqueMod_6;
            public float UniqueMod_6;
            public bool Enable_UniqueMod_7;
            public float UniqueMod_7;

            public bool Enable_LegendaryPotencial;
            public int LegendaryPotencial;

            public bool Enable_WeaverWill;
            public int WeaverWill;
        }

        public struct Headhunter
        {
            public bool WeaverWill;
            public bool BaseDrop;
            public bool UniqueDrop;
        }

        public struct Mjolner
        {
            public bool enable;
            public float MinTriggerChance;
            public float MaxTriggerChance;
            public int IntRequirement;
            public int StrRequirement;
            public bool BaseDrop;
            public bool UniqueDrop;
            public bool WeaverWill;
            public bool ProcAnyLightningSpell;
            public System.Double SocketedCooldown;
            public string SockectedSkill_0;
            public string SockectedSkill_1;
            public string SockectedSkill_2;
        }

        //Factions
        public struct Factions
        {
            public TheWoven TheWoven;
        }

        public struct TheWoven
        {
            public bool Enable_TreePoints;
            public int TreePoints;
            public bool Enable_PointMultiplier;
            public int PointMultiplier;
            public bool Enable_FreeRespe;
        }

        //Scenes
        public struct Scenes
        {
            public Camera Camera;
            public Dungeons Dungeons;
            public Minimap Minimap;
            public Monoliths Monoliths;
        }

        public struct Camera
        {
            public bool Enable_Mod;

            public bool Enable_ZoomMinimum;
            public float ZoomMinimum;

            public bool Enable_ZoomPerScroll;
            public float ZoomPerScroll;

            public bool Enable_ZoomSpeed;
            public float ZoomSpeed;

            public bool Enable_DefaultRotation;
            public float DefaultRotation;

            public bool Enable_OffsetMinimum;
            public float OffsetMinimum;

            public bool Enable_OffsetMaximum;
            public float OffsetMaximum;

            public bool Enable_AngleMinimum;
            public float AngleMinimum;

            public bool Enable_AngleMaximum;
            public float AngleMaximum;

            public bool Enable_LoadOnStart;
        }

        public struct Dungeons
        {
            public bool Enable_EnterWithoutKey;
        }

        public struct Minimap
        {
            public bool Enable_MaxZoomOut;
            public bool Enable_RemoveFogOfWar;
        }

        public struct Monoliths
        {
            public bool Enable_MaxStability;
            public float MaxStability;

            public int Monoliths_Corruption;

            public bool Enable_MobsDensity;
            public float MobsDensity;

            public bool Enable_MobsDefeatOnStart;
            public float MobsDefeatOnStart;

            public bool Enable_BlessingSlots;
            public int BlessingSlots;

            public bool Enable_MaxStabilityOnStart;
            public bool Enable_MaxStabilityOnStabilityChanged;
            public bool Enable_ObjectiveReveal;
            public bool Enable_DropShadeGauntlet;
            public bool Enable_CompleteObjective;
            public bool Enable_NoLostWhenDie;
            public Monoliths_Corruptions Corruptions;
            public bool Enable_Islands;
        }

        public struct Monoliths_Corruptions
        {
            public bool Enable_Monoliths_Corruptions;
            public int Fall_Of_The_Outcast;
            public int The_Stolen_Lance;
            public int The_Black_Sun;
            public int Blood_Frost_And_Death;
            public int Ending_The_Storm;
            public int Fall_Of_The_Empire;
            public int Reign_Of_Dragon;
            public int The_Last_Ruins;
            public int The_Age_Of_Winter;
            public int Spirits_Of_Fire;
        }

        //Skills
        public struct Skills
        {
            public bool Enable_RemoveManaCost;
            public bool Enable_RemoveChannelCost;
            public bool Enable_NoManaRegenWhileChanneling;
            public bool Enable_StopWhenOutOfMana;
            public bool Enable_RemoveCooldown;
            public bool Enable_AllSkills;
            public bool Disable_NodeRequirement;

            public bool Enable_SpecializationSlots;
            public float SpecializationSlots;

            public bool Enable_SkillLevel;
            public float SkillLevel;
            public bool Enable_SkillLevelMultiplier;
            public float SkillLevelMultiplier;

            public bool Enable_PassivePoints;
            public float PassivePoints;
            public bool Enable_PassivePointMultiplier;
            public float PassivePointMultiplier;

            public MovementSkills MovementSkills;
            public SkillTree SkillTree;
            public PassiveTree PassiveTree;
            public Companion Companion;
            public Minions Minions;
        }

        public struct MovementSkills
        {
            public bool Enable_NoTarget;
            public bool Enable_ImmuneDuringMovement;
            public bool Disable_SimplePath;
        }

        public struct SkillTree
        {
            public bool Enable_Slots;
            public byte Slots;
            public bool Enable_Level;
            public byte Level;
        }

        public struct PassiveTree
        {
            public bool Enable_PointsEarnt;
            public ushort PointsEarnt;
        }

        public struct Companion
        {
            public bool Enable_Limit;
            public int Limit;
            public Wolf Wolf;
            public Scorpion Scorpion;
        }

        public struct Wolf
        {
            public bool Enable_SummonMax;
            public bool Enable_SummonLimit;
            public int SummonLimit;
            public bool Enable_StunImmunity;
        }

        public struct Scorpion
        {
            public bool Enable_BabyQuantity;
            public int BabyQuantity;
        }

        public struct Minions
        {
            public Skeletons Skeletons;
            public Wraiths Wraiths;
            public Mages Mages;
            public BoneGolems BoneGolems;
            public VolatileZombies VolatileZombies;
            public DreadShades DreadShades;
        }

        public struct Skeletons
        {
            public bool Enable_additionalSkeletonsFromPassives;
            public int additionalSkeletonsFromPassives;
            public bool Enable_additionalSkeletonsFromSkillTree;
            public int additionalSkeletonsFromSkillTree;
            public bool Enable_additionalSkeletonsPerCast;
            public int additionalSkeletonsPerCast;
            public bool Enable_chanceToResummonOnDeath;
            public float chanceToResummonOnDeath;
            public bool Enable_forceArcher;
            public bool Enable_forceBrawler;
            public bool Enable_forceRogue;
            public bool Enable_forceWarrior;
        }

        public struct Wraiths
        {
            public bool Enable_additionalMaxWraiths;
            public int additionalMaxWraiths;
            public bool Enable_delayedWraiths;
            public int delayedWraiths;
            public bool Enable_limitedTo2Wraiths;
            public bool Enable_wraithsDoNotDecay;
            public bool Enable_increasedCastSpeed;
            public float increasedCastSpeed;
        }

        public struct Mages
        {
            public bool Enable_additionalSkeletonsFromItems;
            public int additionalSkeletonsFromItems;
            public bool Enable_additionalSkeletonsFromPassives;
            public int additionalSkeletonsFromPassives;
            public bool Enable_additionalSkeletonsFromSkillTree;
            public int additionalSkeletonsFromSkillTree;
            public bool Enable_additionalSkeletonsPerCast;
            public int additionalSkeletonsPerCast;
            public bool Enable_onlySummonOneMage;
            public bool Enable_singleSummon;
            public bool Enable_forceCryomancer;
            public bool Enable_forceDeathKnight;
            public bool Enable_forcePyromancer;
            public bool Enable_doubleProjectiles;
            public bool Enable_chanceForTwoExtraProjectiles;
            public float chanceForTwoExtraProjectiles;
        }

        public struct BoneGolems
        {
            public bool Enable_addedGolemsPer4Skeletons;
            public int addedGolemsPer4Skeletons;
            public bool Enable_twins;
            public bool Enable_hasSlamAttack;
            public bool Enable_selfResurrectChance;
            public float selfResurrectChance;
            public bool Enable_increasedFireAuraArea;
            public float increasedFireAuraArea;
            public bool Enable_increasedMoveSpeed;
            public float increasedMoveSpeed;
            public bool Enable_undeadArmorAura;
            public float undeadArmorAura;
            public bool Enable_undeadMovespeedAura;
            public float undeadMovespeedAura;
        }

        public struct VolatileZombies
        {
            public bool Enable_chanceToCastFromMinionDeath;
            public float chanceToCastFromMinionDeath;
            public bool Enable_chanceToCastInfernalShadeOnDeath;
            public float chanceToCastInfernalShadeOnDeath;
            public bool Enable_chanceToCastMarrowShardsOnDeath;
            public float chanceToCastMarrowShardsOnDeath;
        }

        public struct DreadShades
        {
            public bool Enable_Duration;
            public float Duration;
            public bool Enable_DisableLimit;
            public bool Enable_DisableHealthDrain;
            public bool Enable_Max;
            public int max;
            public bool Enable_ReduceDecay;
            public float decay;
            public bool Enable_Radius;
            public float radius;
        }
    }
}
