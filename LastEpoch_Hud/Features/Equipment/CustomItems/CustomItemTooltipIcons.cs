using System.Collections.Generic;
using Il2Cpp;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

/// <summary>Keeps our icon on the item and comparison tooltips that show a custom unique.</summary>
public static class CustomItemTooltipIcons
{
    private static readonly List<CustomItemTooltipBinding> _bindings = new();

    /// <summary>Replaces the tooltip's binding and applies the icon when the item is a custom unique.</summary>
    public static void Bind(UITooltipItem ui, ItemData item, bool comparison)
    {
        if (ui.IsNullOrDestroyed())
        {
            return;
        }

        Unbind(ui, comparison);
        int index = CustomUniqueItems.IndexOf(item);
        if (index < 0)
        {
            return;
        }

        Image[] images = Images(ui, comparison);
        _bindings.Add(
            new CustomItemTooltipBinding
            {
                Owner = ui,
                Comparison = comparison,
                Images = images,
                IconIndex = index,
            }
        );
        Apply(images, index);
    }

    /// <summary>Native image loads can finish after our postfix, so re-apply on tracked active tooltips.</summary>
    public static void Refresh()
    {
        for (int i = _bindings.Count - 1; i >= 0; i--)
        {
            RefreshAt(i);
        }
    }

    private static void RefreshAt(int i)
    {
        CustomItemTooltipBinding binding = _bindings[i];
        if (binding.Owner.IsNullOrDestroyed())
        {
            _bindings.RemoveAt(i);
            return;
        }

        if (!binding.Owner.gameObject.activeInHierarchy)
        {
            return;
        }
        Apply(binding.Images, binding.IconIndex);
    }

    private static void Unbind(UITooltipItem ui, bool comparison)
    {
        for (int i = _bindings.Count - 1; i >= 0; i--)
        {
            if (_bindings[i].Owner == ui && _bindings[i].Comparison == comparison)
            {
                _bindings.RemoveAt(i);
            }
        }
    }

    private static void Apply(Image[] images, int index)
    {
        foreach (Image image in images)
        {
            CustomItemIcons.Apply(image, index);
        }
    }

    private static Image[] Images(UITooltipItem ui, bool comparison)
    {
        if (comparison)
        {
            return new[]
            {
                ui.compareItemImage,
                ui.compareSmallItemImage,
                ui.compareMediumItemImage,
                ui.compareTallMediumItemImage,
                ui.compareLargeItemImage,
                ui.compareSpearItemImage,
                ui.compareWideItemImage,
            };
        }

        return new[]
        {
            ui.itemImage,
            ui.smallItemImage,
            ui.mediumItemImage,
            ui.tallMediumItemImage,
            ui.largeItemImage,
            ui.spearItemImage,
            ui.wideItemImage,
        };
    }
}
