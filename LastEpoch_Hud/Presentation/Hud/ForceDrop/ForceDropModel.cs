using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.ForceDrop;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.ForceDrop;

/// <summary>Force Drop catalog selection backed by the hidden prefab dropdowns.</summary>
internal static class ForceDropModel
{
    public static bool Initialized;
    public static bool Enabled;

    public static GameObject ContentObject;

    public static Dropdown TypeDropdown;
    public static int ItemType = -1;
    public static bool TypeInitialized;
    public static bool InitializingType;

    public static Dropdown RarityDropdown;
    public static int ItemRarity = -1;

    public static Dropdown ItemsDropdown;
    public static int ItemSubtype = -1;
    public static int ItemUniqueId = -1;

    public static UniqueList.LegendaryType ItemLegendaryType = UniqueList
        .LegendaryType
        .LegendaryPotential;

    private static GameObject _legacyRoot;
    private static List<int> _typeOptions = new();
    private static List<ForceDropRarity> _rarityOptions = new();
    private static List<ForceDropItemOption> _itemOptions = new();

    public static void GetRefs(GameObject hud)
    {
        _legacyRoot = Functions.GetChild(hud, "Content");
        if (_legacyRoot.IsNullOrDestroyed())
        {
            return;
        }
        ContentObject = Functions.GetChild(_legacyRoot, "Old_ForceDrop_Content");
        if (ContentObject.IsNullOrDestroyed())
        {
            Main.logger_instance.Error("content_obj is null");
            return;
        }
        GameObject leftBaseContent = Functions.GetViewportContent(ContentObject, "Left", "Item");
        if (leftBaseContent.IsNullOrDestroyed())
        {
            Main.logger_instance.Error("left_obj not found");
            return;
        }
        TypeDropdown = Functions.Get_DopboxInPanel(
            leftBaseContent,
            "Type",
            "Dropdown_Items_ForceDrop_Type",
            new System.Action<int>(
                (_) =>
                {
                    SelectType();
                }
            )
        );
        RarityDropdown = Functions.Get_DopboxInPanel(
            leftBaseContent,
            "Rarity",
            "Dropdown_Items_ForceDrop_Rarity",
            new System.Action<int>(
                (_) =>
                {
                    SelectRarity();
                }
            )
        );
        ItemsDropdown = Functions.Get_DopboxInPanel(
            leftBaseContent,
            "Item",
            "Dropdown_Items_ForceDrop_Item",
            new System.Action<int>(
                (_) =>
                {
                    SelectItem();
                }
            )
        );
        if (TypeDropdown.IsNullOrDestroyed())
        {
            Main.logger_instance.Error("Error type_dropdown not found");
        }
        if (RarityDropdown.IsNullOrDestroyed())
        {
            Main.logger_instance.Error("Error rarity_dropdown not found");
        }
        if (ItemsDropdown.IsNullOrDestroyed())
        {
            Main.logger_instance.Error("Error items_dropdown not found");
        }
        Initialized =
            !TypeDropdown.IsNullOrDestroyed()
            && !RarityDropdown.IsNullOrDestroyed()
            && !ItemsDropdown.IsNullOrDestroyed();
    }

    /// <summary>Hides every prefab child of the Force Drop page once.</summary>
    public static void HideLegacyControls()
    {
        if (ContentObject.IsNullOrDestroyed())
        {
            return;
        }
        foreach (GameObject child in Functions.GetAllChild(ContentObject))
        {
            child.SetActive(false);
        }
    }

    /// <summary>Shows or hides the Force Drop panel and the legacy root around it.</summary>
    public static void SetActive(bool show)
    {
        Enabled = show && !ContentObject.IsNullOrDestroyed();
        SetObjectActive(_legacyRoot, Enabled);
        SetObjectActive(ContentObject, Enabled);
    }

    public static void InitForceDrop()
    {
        if (TypeInitialized || InitializingType)
        {
            return;
        }
        InitializingType = true;
        try
        {
            TypeDropdown.ClearOptions();
            Il2CppSystem.Collections.Generic.List<Dropdown.OptionData> options =
                PlaceholderOptions();
            List<int> types = new();
            foreach (ItemList.BaseEquipmentItem item in ItemList.get().EquippableItems)
            {
                options.Add(new Dropdown.OptionData { text = item.BaseTypeName });
                types.Add(item.baseTypeID);
            }
            foreach (ItemList.BaseNonEquipmentItem item in ItemList.get().nonEquippableItems)
            {
                options.Add(new Dropdown.OptionData { text = item.BaseTypeName });
                types.Add(item.baseTypeID);
            }
            TypeDropdown.options = options;
            _typeOptions = types;
            TypeDropdown.value = 0;

            RarityDropdown.ClearOptions();
            RarityDropdown.enabled = false;

            ItemsDropdown.ClearOptions();
            ItemsDropdown.enabled = false;

            TypeInitialized = true;
        }
        catch (System.Exception ex)
        {
            ErrorLog.Report(ex, "ForceDropModel.InitForceDrop");
        }
        InitializingType = false;
    }

    public static void SelectType()
    {
        if (!TypeInitialized)
        {
            return;
        }
        ItemType = ForceDropSelection.TryOptionAt(_typeOptions, TypeDropdown.value, out int type)
            ? type
            : -1;
        UpdateRarity();
        UpdateItems();
    }

    public static void UpdateRarity()
    {
        if (!CanUpdateCatalog())
        {
            return;
        }
        List<ForceDropRarity> rarities = ForceDropSelection.Rarities(ItemType, SnapshotUniques());
        Il2CppSystem.Collections.Generic.List<Dropdown.OptionData> options = PlaceholderOptions();
        foreach (ForceDropRarity rarity in rarities)
        {
            options.Add(new Dropdown.OptionData { text = RarityLabel(rarity) });
        }
        RarityDropdown.options = options;
        _rarityOptions = rarities;
        RarityDropdown.enabled = rarities.Count > 0;
        RarityDropdown.value = 0;
        ItemRarity = -1;
    }

    public static void SelectRarity()
    {
        if (!TypeInitialized)
        {
            return;
        }
        ItemRarity = ForceDropSelection.TryOptionAt(
            _rarityOptions,
            RarityDropdown.value,
            out ForceDropRarity rarity
        )
            ? ForceDropSelection.GameRarity(rarity)
            : -1;
        UpdateItems();
    }

    public static void UpdateItems()
    {
        if (!CanUpdateCatalog())
        {
            return;
        }
        if (
            !ForceDropSelection.TryOptionAt(
                _rarityOptions,
                RarityDropdown.value,
                out ForceDropRarity rarity
            )
        )
        {
            ItemsDropdown.options = PlaceholderOptions();
            ItemsDropdown.enabled = false;
            _itemOptions.Clear();
            ItemsDropdown.value = 0;
            return;
        }
        List<ForceDropItemOption> items = ForceDropSelection.Items(
            ItemType,
            rarity,
            SnapshotBaseItems(),
            SnapshotUniques()
        );
        Il2CppSystem.Collections.Generic.List<Dropdown.OptionData> options = PlaceholderOptions();
        foreach (ForceDropItemOption item in items)
        {
            options.Add(new Dropdown.OptionData { text = item.Label });
        }
        ItemsDropdown.options = options;
        _itemOptions = items;
        ItemsDropdown.enabled = true;
        ItemsDropdown.value = 0;
    }

    public static void SelectItem()
    {
        if (!TypeInitialized)
        {
            return;
        }
        ItemSubtype = -1;
        ItemUniqueId = 0;
        if (
            !ForceDropSelection.TryOptionAt(
                _itemOptions,
                ItemsDropdown.value,
                out ForceDropItemOption item
            )
        )
        {
            return;
        }
        ItemSubtype = item.SubType;
        ItemUniqueId = item.UniqueId;
        if (item.Rarity == ForceDropRarity.Base)
        {
            return;
        }
        ItemLegendaryType = LegendaryTypeOf(item.UniqueId);
    }

    private static bool CanUpdateCatalog()
    {
        return Enabled
            && LastEpoch_Hud.Scenes.IsGameScene()
            && !Refs_Manager.item_list.IsNullOrDestroyed()
            && TypeInitialized
            && !TypeDropdown.IsNullOrDestroyed()
            && !RarityDropdown.IsNullOrDestroyed()
            && !ItemsDropdown.IsNullOrDestroyed();
    }

    private static Il2CppSystem.Collections.Generic.List<Dropdown.OptionData> PlaceholderOptions()
    {
        var options = new Il2CppSystem.Collections.Generic.List<Dropdown.OptionData>();
        options.Add(new Dropdown.OptionData { text = "Select" });
        return options;
    }

    private static string RarityLabel(ForceDropRarity rarity)
    {
        return rarity switch
        {
            ForceDropRarity.Unique => "Unique",
            ForceDropRarity.Set => "Set",
            _ => "Base Item",
        };
    }

    private static List<ForceDropItemOption> SnapshotBaseItems()
    {
        var result = new List<ForceDropItemOption>();
        foreach (ItemList.BaseEquipmentItem baseItem in ItemList.get().EquippableItems)
        {
            foreach (ItemList.EquipmentItem item in baseItem.subItems)
            {
                result.Add(
                    BaseOption(baseItem.baseTypeID, item.subTypeID, item.displayName, item.name)
                );
            }
        }
        foreach (ItemList.BaseNonEquipmentItem baseItem in ItemList.get().nonEquippableItems)
        {
            foreach (ItemList.NonEquipmentItem item in baseItem.subItems)
            {
                result.Add(
                    BaseOption(baseItem.baseTypeID, item.subTypeID, item.displayName, item.name)
                );
            }
        }
        return result;
    }

    private static ForceDropItemOption BaseOption(
        int baseType,
        int subType,
        string display,
        string internalName
    )
    {
        return new ForceDropItemOption(
            baseType,
            subType,
            ForceDropRarity.Base,
            0,
            ForceDropSelection.CatalogLabel(display, internalName)
        );
    }

    private static List<ForceDropUniqueEntry> SnapshotUniques()
    {
        var result = new List<ForceDropUniqueEntry>();
        if (UniqueList.instance.IsNullOrDestroyed())
        {
            UniqueList.getUnique(0);
        }
        if (UniqueList.instance.IsNullOrDestroyed())
        {
            return result;
        }
        foreach (UniqueList.Entry unique in UniqueList.instance.uniques)
        {
            result.Add(
                new ForceDropUniqueEntry(
                    unique.uniqueID,
                    unique.baseType,
                    unique.isSetItem,
                    SubTypesOf(unique),
                    ForceDropSelection.CatalogLabel(unique.displayName, unique.name)
                )
            );
        }
        return result;
    }

    private static int[] SubTypesOf(UniqueList.Entry unique)
    {
        if (unique.subTypes.IsNullOrDestroyed())
        {
            return System.Array.Empty<int>();
        }
        int[] result = new int[unique.subTypes.Count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = unique.subTypes[i];
        }
        return result;
    }

    private static UniqueList.LegendaryType LegendaryTypeOf(int uniqueId)
    {
        UniqueList.Entry entry = UniqueList.getUnique((ushort)uniqueId);
        return entry.IsNullOrDestroyed()
            ? UniqueList.LegendaryType.LegendaryPotential
            : entry.legendaryType;
    }

    private static void SetObjectActive(GameObject target, bool show)
    {
        if (target.IsNullOrDestroyed())
        {
            return;
        }

        target.SetActive(show);
    }
}
