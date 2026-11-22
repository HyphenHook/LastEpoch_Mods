using System;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

/// <summary>Adds one custom unique to the game's item and unique lists.</summary>
public sealed class CustomUniqueRegistrar
{
    private readonly CustomUniqueDefinition _definition;
    private readonly CustomUniqueProgress _progress;
    private readonly string _context;
    private UniqueList.Entry _entry;

    public CustomUniqueRegistrar(CustomUniqueDefinition definition)
    {
        _definition = definition;
        _progress = new CustomUniqueProgress(definition.Spec.AddsBase);
        _context = definition.Spec.Name + " registration";
        BaseId = definition.Spec.BaseId;
    }

    /// <summary>Subtype id in use; the allocated one when the spec asks for a free id.</summary>
    public int BaseId { get; private set; }

    public bool IsRegistered => _progress.Next(true) == CustomUniqueStep.Done;

    public void Update()
    {
        if (_progress.IsFinished)
        {
            return;
        }

        try
        {
            _progress.RunPending(GameReady(), RunStep);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, _context);
            _progress.Fail();
        }
    }

    private static bool GameReady()
    {
        return Locales.current != Locales.Selected.Unknow
            && !Save_Manager.instance.IsNullOrDestroyed()
            && Save_Manager.instance.initialized
            && !Refs_Manager.item_list.IsNullOrDestroyed()
            && !Refs_Manager.unique_list.IsNullOrDestroyed();
    }

    private void RunStep(CustomUniqueStep step)
    {
        switch (step)
        {
            case CustomUniqueStep.AddBase:
                AddBase();
                break;
            case CustomUniqueStep.AddUnique:
                AddUnique();
                break;
            case CustomUniqueStep.AddToDictionary:
                AddToDictionary();
                break;
        }
    }

    private void AddBase()
    {
        Il2CppSystem.Collections.Generic.List<ItemList.EquipmentItem> subItems = Refs_Manager
            .item_list
            .EquippableItems[_definition.Spec.BaseType]
            .subItems;
        int baseId = CustomItemIds.PickBaseId(
            _definition.Spec.BaseId,
            subItems.Count,
            UsedSubtypeIds(subItems)
        );
        if (baseId == CustomItemIds.None)
        {
            Fail("no free subtype id for base type " + _definition.Spec.BaseType);
            return;
        }

        BaseId = baseId;
        CustomItemLocalization.Table.RegisterLocaleKey(
            CustomItemKeys.SubtypeName(_definition.Spec.BaseType, BaseId),
            _definition.SubtypeNameKey
        );
        subItems.Add(CreateBase(_definition.Flags()));
        _progress.Complete();
    }

    private void AddUnique()
    {
        UniqueList.getUnique(0); // force the game to initialize its unique list
        if (Refs_Manager.unique_list.entryDictionary.ContainsKey(_definition.Spec.UniqueId))
        {
            Fail("unique id " + _definition.Spec.UniqueId + " is already used");
            return;
        }

        RegisterTexts();
        _entry = CreateEntry(_definition.Flags());
        Refs_Manager.unique_list.uniques.Add(_entry);
        _progress.Complete();
    }

    private void AddToDictionary()
    {
        if (Refs_Manager.unique_list.entryDictionary.ContainsKey(_definition.Spec.UniqueId))
        {
            Fail("unique id " + _definition.Spec.UniqueId + " is already in the dictionary");
            return;
        }

        Refs_Manager.unique_list.entryDictionary.Add(_definition.Spec.UniqueId, _entry);
        CustomUniqueItems.Subtypes.Set(_definition.Spec.UniqueId, BaseId);
        _progress.Complete();
    }

    private void RegisterTexts()
    {
        CustomItemTextTable table = CustomItemLocalization.Table;
        ushort uniqueId = _definition.Spec.UniqueId;
        table.RegisterLocaleKey(CustomItemKeys.UniqueName(uniqueId), _definition.UniqueNameKey);
        table.RegisterLocaleKey(CustomItemKeys.UniqueLore(uniqueId), _definition.LoreKey);
        if (_definition.Description == null)
        {
            return;
        }

        table.Register(CustomItemKeys.UniqueTooltip(uniqueId), _definition.Description);
    }

    private ItemList.EquipmentItem CreateBase(CustomUniqueFlags flags)
    {
        return new ItemList.EquipmentItem
        {
            classRequirement = ItemList.ClassRequirement.None,
            implicits = CustomItemAffixLists.Implicits(_definition.Implicits ?? Array.Empty<Core.CustomItems.Affixes.CustomBaseImplicit>()),
            subClassRequirement = ItemList.SubClassRequirement.None,
            cannotDrop = flags.BaseCannotDrop,
            itemTags = ItemLocationTag.None,
            levelRequirement = _definition.Spec.LevelRequirement,
            name = CustomItemLocalization.Text(
                CustomItemKeys.SubtypeName(_definition.Spec.BaseType, BaseId)
            ),
            subTypeID = BaseId,
        };
    }

    private UniqueList.Entry CreateEntry(CustomUniqueFlags flags)
    {
        CustomUniqueSpec spec = _definition.Spec;
        string name = CustomItemLocalization.Resolve(CustomItemKeys.UniqueName(spec.UniqueId)) ?? spec.Name;
        var entry = new UniqueList.Entry
        {
            name = name,
            displayName = name,
            uniqueID = spec.UniqueId,
            isSetItem = false,
            setID = 0,
            overrideLevelRequirement = spec.OverrideLevelRequirement,
            levelRequirement = (byte)spec.LevelRequirement,
            legendaryType = LegendaryTypeFor(flags),
            overrideEffectiveLevelForLegendaryPotential = true,
            effectiveLevelForLegendaryPotential = (byte)spec.EffectiveLevelForLegendaryPotential,
            canDropRandomly = flags.CanDropRandomly,
            rerollChance = 1,
            itemModelType = UniqueList.ItemModelType.Unique,
            subTypeForIM = 0,
            baseType = spec.BaseType,
            subTypes = SubTypes(),
            mods = CustomItemAffixLists.Mods(_definition.Mods),
            loreText = CustomItemLocalization.Text(CustomItemKeys.UniqueLore(spec.UniqueId)),
            tooltipEntries = CustomItemAffixLists.TooltipEntries(_definition.TooltipEntries()),
            oldSubTypeID = 0,
            oldUniqueID = 0,
        };
        AddDescription(entry);
        return entry;
    }

    private void AddDescription(UniqueList.Entry entry)
    {
        if (_definition.Description == null)
        {
            return;
        }

        string description = CustomItemLocalization.Resolve(
            CustomItemKeys.UniqueTooltip(_definition.Spec.UniqueId)
        );
        if (string.IsNullOrEmpty(description))
        {
            return;
        }

        entry.tooltipDescriptions =
            new Il2CppSystem.Collections.Generic.List<ItemTooltipDescription>();
        entry.tooltipDescriptions.Add(new ItemTooltipDescription { description = description });
    }

    private Il2CppSystem.Collections.Generic.List<byte> SubTypes()
    {
        var subTypes = new Il2CppSystem.Collections.Generic.List<byte>();
        subTypes.Add((byte)BaseId);
        return subTypes;
    }

    private static UniqueList.LegendaryType LegendaryTypeFor(CustomUniqueFlags flags)
    {
        return flags.WeaversWill
            ? UniqueList.LegendaryType.WeaversWill
            : UniqueList.LegendaryType.LegendaryPotential;
    }

    private static System.Collections.Generic.HashSet<int> UsedSubtypeIds(
        Il2CppSystem.Collections.Generic.List<ItemList.EquipmentItem> subItems
    )
    {
        var used = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < subItems.Count; i++)
        {
            used.Add(subItems[i].subTypeID);
        }

        return used;
    }

    private void Fail(string reason)
    {
        ErrorLog.Report(reason, _context);
        _progress.Fail();
    }
}
