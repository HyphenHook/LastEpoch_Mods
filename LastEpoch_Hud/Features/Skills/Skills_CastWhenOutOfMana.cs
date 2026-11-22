using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Skills;
using LastEpoch_Hud.Scripts.ModUI;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Skills;

internal static class Skills_CastWhenOutOfMana
{
    // The player mana whose canGoNegative this mod manages, and the character's own value to
    // restore once negative mode is off.
    private static System.IntPtr _managedMana = System.IntPtr.Zero;
    private static bool _originalCanGoNegative = false;

    // The ability whose short cast the mod started, until that cast's spend runs. The game can
    // report the start as failed and still cast a moment later, so it lives a few seconds.
    private static System.IntPtr _overriddenAbility = System.IntPtr.Zero;
    private static float _overrideExpires = 0f;

    // Paces forced casts to the character's own cast speed (see OutOfManaCastPacer).
    private static readonly OutOfManaCastPacer _pacer = new();

    // Where the player was at the last cast, to tell whether they've moved since.
    private static Vector3 _lastCastPosition = Vector3.zero;

    private static bool AnyMode() =>
        ModSettings.SkillsMana.CastWhenOutOfMana.Value
        || ModSettings.SkillsMana.AllowNegativeMana.Value;

    private static BaseMana PlayerMana() =>
        Refs_Manager.player_actor.IsNullOrDestroyed() ? null : Refs_Manager.player_actor.mana;

    /// <summary>Whether the player is giving movement input (keyboard, mouse or stick).</summary>
    private static bool MovementHeld()
    {
        EpochInputManager input = Refs_Manager.epoch_input_manager;
        if (input.IsNullOrDestroyed())
        {
            return false;
        }
        return input.isKeyboardMovementButtonHeld
            || input.isMouseMovementButtonHeld
            || input.input.sqrMagnitude > 0.01f;
    }

    private static bool IsShort(BaseMana mana, float cost) =>
        !mana.IsNullOrDestroyed() && mana.currentMana < cost;

    /// <summary>The cast's real mana cost, from the game's own BaseMana.getManaCost.</summary>
    private static float CostOf(BaseMana mana, Ability ability)
    {
        try
        {
            return mana.IsNullOrDestroyed() || ability.IsNullOrDestroyed()
                ? 0f
                : mana.getManaCost(ability);
        }
        catch (System.Exception)
        {
            return 0f;
        }
    }

    private static bool IsPlayer(UsingAbility usingAbility)
    {
        BaseMana player = PlayerMana();
        BaseMana mana = usingAbility.baseMana;
        return !player.IsNullOrDestroyed()
            && !mana.IsNullOrDestroyed()
            && player.Pointer == mana.Pointer;
    }

    [HarmonyPatch(typeof(UsingAbilityPlayer), nameof(UsingAbilityPlayer.InitialiseAbilityUse))]
    public class UsingAbilityPlayer_InitialiseAbilityUse
    {
        [HarmonyPrefix]
        static void Prefix(UsingAbilityPlayer __instance, Ability __0, Vector3 __1, ref bool __2)
        {
            try
            {
                BaseMana mana = __instance.baseMana;
                if (mana.IsNullOrDestroyed())
                {
                    return;
                }
                ApplyCanGoNegative(mana);
                if (__2 || !AnyMode() || __0.IsNullOrDestroyed())
                {
                    return;
                }
                float cost = CostOf(mana, __0);
                if (!IsShort(mana, cost))
                {
                    return;
                }
                ChargeManager charges = __instance.chargeManager;
                if (!charges.IsNullOrDestroyed() && charges.onCoooldown(__0))
                {
                    return;
                }
                bool ready = _pacer.IsReady(
                    __0.Pointer.ToInt64(),
                    Time.time,
                    __instance.midAbilityAnimation,
                    MovementHeld(),
                    Vector3.Distance(__instance.transform.position, _lastCastPosition)
                );
                if (!ready)
                {
                    return;
                }
                AbilityInfo info = __instance.mutatorManager?.getMutatorOrAbility(__0);
                if (
                    info == null
                    || !__instance.canUseAbilityChecksAfterManaAndCooldown(__0, info, __1)
                )
                {
                    return;
                }
                __2 = true;
                _overriddenAbility = __0.Pointer;
                _pacer.ForcedCastStarted(Time.time);
                _overrideExpires = Time.time + 3f;
            }
            catch (System.Exception ex)
            {
                ErrorLog.Report(ex, "Skills_CastWhenOutOfMana");
            }
        }

        /// <summary>
        /// "Allow Negative Mana" turns the game's canGoNegative on for the player's mana; otherwise
        /// the character's own value (gear can grant it) is put back and left alone.
        /// </summary>
        private static void ApplyCanGoNegative(BaseMana mana)
        {
            if (ModSettings.SkillsMana.AllowNegativeMana.Value)
            {
                if (_managedMana != mana.Pointer)
                {
                    _managedMana = mana.Pointer;
                    _originalCanGoNegative = mana.canGoNegative;
                }
                mana.canGoNegative = true;
            }
            else if (_managedMana == mana.Pointer)
            {
                mana.canGoNegative = _originalCanGoNegative;
                _managedMana = System.IntPtr.Zero;
            }
        }
    }

    /// <summary>
    /// Every player cast passes here: its time and duration pace later forced casts. For a cast
    /// the mod forced, the game's spend is skipped (useMana = false), and negative mode charges
    /// the cost afterwards through the game's reduceMana.
    /// </summary>
    [HarmonyPatch(typeof(UsingAbility), nameof(UsingAbility.UseAbility))]
    public class UsingAbility_UseAbility
    {
        [HarmonyPrefix]
        static void Prefix(UsingAbility __instance, Ability __0, ref bool __2, out float __state)
        {
            // The cost negative mode still has to charge after the cast; 0 = nothing to charge.
            __state = 0f;
            if (__0.IsNullOrDestroyed() || !IsPlayer(__instance))
            {
                return;
            }
            bool forced = _overriddenAbility == __0.Pointer && Time.time <= _overrideExpires;
            if (!forced)
            {
                return;
            }
            _pacer.ForcedCastReleased(__0.Pointer.ToInt64(), Time.time);
            if (!__2 || !AnyMode())
            {
                return;
            }
            _overriddenAbility = System.IntPtr.Zero;
            __2 = false;
            if (ModSettings.SkillsMana.AllowNegativeMana.Value)
            {
                __state = CostOf(__instance.baseMana, __0);
            }
        }

        [HarmonyPostfix]
        static void Postfix(UsingAbility __instance, Ability __0, float __state)
        {
            if (__0.IsNullOrDestroyed() || !IsPlayer(__instance))
            {
                return;
            }
            // The cast's duration is set once it has started; pace the next forced cast by it.
            _pacer.CastReleased(Time.time, __instance.castDuration);
            _lastCastPosition = __instance.transform.position;
            if (__state <= 0f)
            {
                return;
            }
            try
            {
                BaseMana mana = __instance.baseMana;
                if (!mana.IsNullOrDestroyed())
                {
                    mana.reduceMana(__state);
                }
            }
            catch (System.Exception ex)
            {
                ErrorLog.Report(ex, "Skills_CastWhenOutOfMana");
            }
        }
    }
}
