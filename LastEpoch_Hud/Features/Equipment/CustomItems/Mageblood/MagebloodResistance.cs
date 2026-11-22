using System;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood;

/// <summary>Applies the Mageblood max res bonus to the local player's resisted damage.</summary>
internal static class MagebloodResistance
{
    public static void Apply(
        ProtectionClass instance,
        float damage,
        DamageType type,
        ref float resisted
    )
    {
        if (!MagebloodBuffs.Worn)
        {
            return;
        }
        try
        {
            if (!MagebloodMaxResistance.AppliesTo(instance.Pointer, PlayerPointer()))
            {
                return;
            }
            resisted = MagebloodMaxResistance.Rescale(
                resisted,
                damage,
                Uncapped(instance, type),
                MagebloodConfigLoader.Current.MaxResistances
            );
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "MagebloodResistance.Apply");
        }
    }

    private static IntPtr PlayerPointer()
    {
        ProtectionClass player = Refs_Manager.player_protection_class;
        return player.IsNullOrDestroyed() ? IntPtr.Zero : player.Pointer;
    }

    private static float Uncapped(ProtectionClass instance, DamageType type)
    {
        return type switch
        {
            DamageType.PHYSICAL => instance.uncappedPhysicalResistance,
            DamageType.FIRE => instance.uncappedFireResistance,
            DamageType.COLD => instance.uncappedColdResistance,
            DamageType.LIGHTNING => instance.uncappedLightningResistance,
            DamageType.NECROTIC => instance.uncappedNecroticResistance,
            DamageType.VOID => instance.uncappedVoidResistance,
            DamageType.POISON => instance.uncappedPoisonResistance,
            _ => 0f,
        };
    }
}
