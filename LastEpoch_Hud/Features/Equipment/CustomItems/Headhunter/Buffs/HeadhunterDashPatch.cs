using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

[HarmonyPatch(typeof(MovementFromAbility), nameof(MovementFromAbility.startMoving))]
public class HeadhunterDashPatch
{
    [HarmonyPrefix]
    private static void Prefix(
        MovementFromAbility __instance,
        AbilityMovement _movementType,
        ref float _fixedDistanceDashDistance,
        ref float _maxMovementDistanceFromStart
    )
    {
        HeadhunterDash.Apply(
            __instance,
            _movementType,
            ref _fixedDistanceDashDistance,
            ref _maxMovementDistanceFromStart
        );
    }
}
