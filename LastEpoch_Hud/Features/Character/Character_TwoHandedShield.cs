using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Character;

[RegisterTypeInIl2Cpp]
public class Character_TwoHandedShield : MonoBehaviour
{
    public Character_TwoHandedShield(System.IntPtr ptr)
        : base(ptr) { }

    public static Character_TwoHandedShield instance { get; private set; }

    private static int lastMainHandType = -1;
    private static int lastOffHandType = -1;
    private static bool applied;
    private static Il2CppSystem.Collections.Generic.List<int> originalShieldTypes;
    private static Il2CppSystem.Collections.Generic.List<int> allHandTypes;
    private static Il2CppSystem.Collections.Generic.List<int> allowedTypes;

    void Awake()
    {
        instance = this;
    }

    public static bool Enabled()
    {
        if (
            Save_Manager.instance.IsNullOrDestroyed()
            || Save_Manager.instance.data.IsNullOrDestroyed()
        )
        {
            return false;
        }

        return Save_Manager.instance.data.Character.Cheats.Enable_TwoHandedWithShield;
    }

    static Il2CppSystem.Collections.Generic.List<int> AllowedTypes()
    {
        if (allowedTypes == null)
        {
            allowedTypes = new Il2CppSystem.Collections.Generic.List<int>();
            allowedTypes.Add((int)Il2Cpp.EquipmentType.TWO_HANDED_AXE);
            allowedTypes.Add((int)Il2Cpp.EquipmentType.TWO_HANDED_MACE);
            allowedTypes.Add((int)Il2Cpp.EquipmentType.TWO_HANDED_SPEAR);
            allowedTypes.Add((int)Il2Cpp.EquipmentType.TWO_HANDED_STAFF);
            allowedTypes.Add((int)Il2Cpp.EquipmentType.TWO_HANDED_SWORD);
            allowedTypes.Add((int)Il2Cpp.EquipmentType.BOW);
            allowedTypes.Add((int)Il2Cpp.EquipmentType.CROSSBOW);
        }

        return allowedTypes;
    }

    void Update()
    {
        if (Enabled())
        {
            if (!applied)
                originalShieldTypes = Il2Cpp.CharacterMutator.global2hWeaponWithShieldBaseTypes;
            Il2Cpp.CharacterMutator.global2hWeaponWithShieldBaseTypes = AllowedTypes();
            applied = true;
        }
        else if (applied)
        {
            Il2Cpp.CharacterMutator.global2hWeaponWithShieldBaseTypes = originalShieldTypes;
            originalShieldTypes = null;
            applied = false;
        }
    }

    // Keep the legacy component and save key for existing installations.
    static bool IsWeapon(int type) => Il2Cpp.ItemList.isWeapon(type);

    static bool IsHandItem(int type) =>
        IsWeapon(type)
        || type == (int)Il2Cpp.EquipmentType.SHIELD
        || type == (int)Il2Cpp.EquipmentType.CATALYST
        || type == (int)Il2Cpp.EquipmentType.QUIVER;

    static bool IsTwoHander(int type) => AllowedTypes().Contains(type);

    static Il2CppSystem.Collections.Generic.List<int> AllHandTypes()
    {
        if (allHandTypes == null)
        {
            allHandTypes = new Il2CppSystem.Collections.Generic.List<int>();
            foreach (
                Il2Cpp.EquipmentType type in System.Enum.GetValues(typeof(Il2Cpp.EquipmentType))
            )
                if (IsHandItem((int)type))
                    allHandTypes.Add((int)type);
        }
        return allHandTypes;
    }

    [HarmonyPatch(
        typeof(Il2Cpp.CharacterMutator),
        "AdditionalBaseTypeProvider_getAdditionalBaseTypes"
    )]
    public class CharacterMutator_AdditionalOffhandTypes
    {
        [HarmonyPostfix]
        static void Postfix(
            Il2Cpp.ContainerID __0,
            ref Il2CppSystem.Collections.Generic.List<int> __result
        )
        {
            if (Enabled() && __0 == Il2Cpp.ContainerID.EQ_OFFHAND)
                __result = AllHandTypes();
        }
    }

    [HarmonyPatch(typeof(Il2Cpp.SimpleAdditionalBaseTypeProvider), "getAdditionalBaseTypes")]
    public class SimpleProvider_AdditionalOffhandTypes
    {
        [HarmonyPostfix]
        static void Postfix(
            Il2Cpp.ContainerID __0,
            ref Il2CppSystem.Collections.Generic.List<int> __result
        )
        {
            if (Enabled() && __0 == Il2Cpp.ContainerID.EQ_OFFHAND)
                __result = AllHandTypes();
        }
    }

    // Change type acceptance only; the container still checks level/class,
    // faction and item-count restrictions through its normal receive path.
    [HarmonyPatch(typeof(Il2Cpp.OneSlotItemContainer), "CanAddItemType")]
    public class OneSlotItemContainer_HandTypes
    {
        [HarmonyPostfix]
        static void Postfix(Il2Cpp.OneSlotItemContainer __instance, int __0, ref bool __result)
        {
            if (
                Enabled()
                && __instance.GetContainerID() == Il2Cpp.ContainerID.EQ_OFFHAND
                && IsHandItem(__0)
            )
                __result = true;
        }
    }

    [HarmonyPatch(typeof(Il2Cpp.ItemContainersManager), "CanEquipInOffhand")]
    public class ItemContainersManager_OffhandTypes
    {
        [HarmonyPostfix]
        static void Postfix(Il2Cpp.ItemContainerEntry __0, ref bool __result)
        {
            if (
                Enabled()
                && !__0.IsNullOrDestroyed()
                && !__0.data.IsNullOrDestroyed()
                && IsHandItem(__0.data.itemType)
            )
                __result = true;
        }
    }

    [HarmonyPatch]
    public class PaperDollContainer_HandCompatibility
    {
        static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            // This wrapper is absent in some generated assembly sets.
            // Resolve its native compatibility method without a compile-time dependency.
            foreach (var type in typeof(Il2Cpp.ItemContainersManager).Assembly.GetTypes())
            {
                if (type.Name != "PaperDollContainer")
                    continue;
                var method = AccessTools.DeclaredMethod(
                    type,
                    "checkWeaponSlotCompatibility",
                    new[] { typeof(Il2Cpp.ItemData), typeof(int) }
                );
                if (method != null)
                    yield return method;
            }
        }

        [HarmonyPrefix]
        static bool Prefix(object __instance, Il2Cpp.ItemData __0, int __1, ref bool __result)
        {
            if (!Enabled() || __0.IsNullOrDestroyed() || __1 < 0)
                return true;
            var containers = AccessTools
                .Property(__instance.GetType(), "Containers")
                ?.GetValue(__instance);
            if (containers == null)
                return true;
            var count = AccessTools.Property(containers.GetType(), "Count")?.GetValue(containers);
            if (!(count is int length) || __1 >= length)
                return true;
            var container = AccessTools
                .Property(containers.GetType(), "Item")
                ?.GetValue(containers, new object[] { __1 });
            if (container == null)
                return true;
            var containerId = AccessTools
                .Method(container.GetType(), "GetContainerID")
                ?.Invoke(container, null);
            if (!(containerId is Il2Cpp.ContainerID id))
                return true;
            if (
                (id == Il2Cpp.ContainerID.EQ_WEAPON && IsWeapon(__0.itemType))
                || (id == Il2Cpp.ContainerID.EQ_OFFHAND && IsHandItem(__0.itemType))
            )
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Il2Cpp.WeaponItemContainer), "EnforceWeaponLimitations")]
    public class WeaponItemContainer_EnforceWeaponLimitations
    {
        [HarmonyPrefix]
        static bool Prefix() => !Enabled();
    }

    [HarmonyPatch(typeof(Il2Cpp.ItemContainersManager), "HasDualWieldingWeaponEquipped")]
    public class ItemContainersManager_DualWieldingWeapon
    {
        [HarmonyPostfix]
        static void Postfix(
            Il2Cpp.ItemContainersManager __instance,
            ref Il2Cpp.ItemContainerEntry __0,
            ref bool __result
        )
        {
            if (
                !Enabled()
                || __result
                || __instance.equipment.IsNullOrDestroyed()
                || __instance.equipment.offhand.IsNullOrDestroyed()
            )
                return;
            if (
                __instance.equipment.offhand.TryGetContent(out var entry)
                && !entry.IsNullOrDestroyed()
                && !entry.data.IsNullOrDestroyed()
                && IsWeapon(entry.data.itemType)
            )
            {
                __0 = entry;
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Il2Cpp.ItemContainersManager), "DualWielding")]
    public class ItemContainersManager_DualWielding
    {
        [HarmonyPostfix]
        static void Postfix(Il2Cpp.ItemContainersManager __instance, ref bool __result)
        {
            if (!Enabled() || __result || __instance.equipment.IsNullOrDestroyed())
                return;
            var main = __instance.equipment.weapon;
            var off = __instance.equipment.offhand;
            if (main.IsNullOrDestroyed() || off.IsNullOrDestroyed())
                return;
            if (
                main.TryGetContentItemData(out var mainItem)
                && off.TryGetContentItemData(out var offItem)
                && !mainItem.IsNullOrDestroyed()
                && !offItem.IsNullOrDestroyed()
            )
                __result = IsWeapon(mainItem.itemType) && IsWeapon(offItem.itemType);
        }
    }

    static bool IsNonMeleeTwoHander(int itemType)
    {
        return itemType == (int)Il2Cpp.EquipmentType.TWO_HANDED_STAFF
            || itemType == (int)Il2Cpp.EquipmentType.BOW
            || itemType == (int)Il2Cpp.EquipmentType.CROSSBOW;
    }

    [HarmonyPatch(
        typeof(Il2Cpp.OffhandItemContainer),
        nameof(Il2Cpp.OffhandItemContainer.IncompatibleDueTo2hWeapon)
    )]
    public class OffhandItemContainer_IncompatibleDueTo2hWeapon
    {
        [HarmonyPrefix]
        static bool Prefix(ref bool __result)
        {
            if (!Enabled())
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(
        typeof(Il2Cpp.ItemContainersManager),
        nameof(Il2Cpp.ItemContainersManager.CannotPutItemInMainHandBecauseOfOffhand)
    )]
    public class ItemContainersManager_CannotPutMainHand
    {
        [HarmonyPrefix]
        static bool Prefix(ref bool __result)
        {
            if (!Enabled())
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(
        typeof(Il2Cpp.ItemContainersManager),
        nameof(Il2Cpp.ItemContainersManager.CannotPutItemInOffhandBecauseOfMainhand)
    )]
    public class ItemContainersManager_CannotPutOffhand
    {
        [HarmonyPrefix]
        static bool Prefix(ref bool __result)
        {
            if (!Enabled())
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(
        typeof(Il2Cpp.ItemContainersManager),
        nameof(Il2Cpp.ItemContainersManager.UpdateOffHandWeaponIfInvalid)
    )]
    public class ItemContainersManager_UpdateOffHandWeaponIfInvalid
    {
        [HarmonyPrefix]
        static bool Prefix()
        {
            return !Enabled();
        }
    }

    // Preserve the off-hand item when the native weapon-combination check runs.
    [HarmonyPatch(typeof(Il2Cpp.OffhandItemContainer), "EnforceWeaponLimitations")]
    public class OffhandItemContainer_EnforceWeaponLimitations
    {
        [HarmonyPrefix]
        static bool Prefix()
        {
            return !Enabled();
        }
    }

    [HarmonyPatch(
        typeof(Il2Cpp.ItemContainersManager),
        nameof(Il2Cpp.ItemContainersManager.EnforceEquipLimitsIfWeaponSlot)
    )]
    public class ItemContainersManager_EnforceEquipLimitsIfWeaponSlot
    {
        [HarmonyPrefix]
        static bool Prefix(Il2Cpp.ContainerID id)
        {
            if (!Enabled())
            {
                return true;
            }

            return id != Il2Cpp.ContainerID.EQ_OFFHAND && id != Il2Cpp.ContainerID.EQ_WEAPON;
        }
    }

    [HarmonyPatch(
        typeof(Il2Cpp.EquipmentVisualsManager),
        nameof(Il2Cpp.EquipmentVisualsManager.EquipWeapon)
    )]
    public class EquipmentVisualsManager_EquipWeapon
    {
        [HarmonyPrefix]
        static bool Prefix(
            Il2Cpp.EquipmentVisualsManager __instance,
            int itemType,
            Il2Cpp.IMSlotType slotType
        )
        {
            if (!Enabled())
            {
                return true;
            }

            if (
                slotType == Il2Cpp.IMSlotType.OffHand
                && (IsTwoHander(itemType) || IsNonMeleeTwoHander(lastMainHandType))
            )
            {
                __instance.RemoveWeapon(true, false); // Visual only; preserve equipped item.
                lastOffHandType = itemType;
                return false;
            }

            return true;
        }

        [HarmonyPostfix]
        static void Postfix(
            Il2Cpp.EquipmentVisualsManager __instance,
            int itemType,
            Il2Cpp.IMSlotType slotType
        )
        {
            if (slotType == Il2Cpp.IMSlotType.MainHand)
            {
                lastMainHandType = itemType;
            }
            else if (slotType == Il2Cpp.IMSlotType.OffHand)
            {
                lastOffHandType = itemType;
            }

            if (!Enabled())
            {
                return;
            }

            if (
                slotType == Il2Cpp.IMSlotType.MainHand
                && IsNonMeleeTwoHander(itemType)
                && lastOffHandType >= 0
            )
            {
                __instance.RemoveWeapon(true, false);
            }
        }
    }

    [HarmonyPatch(
        typeof(Il2Cpp.EquipmentVisualsManager),
        nameof(Il2Cpp.EquipmentVisualsManager.RemoveWeapon)
    )]
    public class EquipmentVisualsManager_RemoveWeapon
    {
        [HarmonyPostfix]
        static void Postfix(bool offHand, bool clearData)
        {
            if (!clearData)
            {
                return;
            }

            if (offHand)
            {
                lastOffHandType = -1;
            }
            else
            {
                lastMainHandType = -1;
            }
        }
    }
}
