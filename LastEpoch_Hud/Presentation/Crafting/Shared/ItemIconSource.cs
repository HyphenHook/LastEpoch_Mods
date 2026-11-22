using System;
using Il2Cpp;
using Il2CppLE.AssetBundles;
using Il2CppLE.AssetManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// Owns an item sprite load and the inactive native widget used to obtain its rarity background.
/// Windows own these sources and decide when to poll, redraw, replace, and dispose them.
/// </summary>
internal sealed class ItemIconSource : IDisposable
{
    internal Sprite Back { get; private set; }
    internal Sprite Content { get; set; }
    internal Color BackColor { get; private set; } = Color.white;
    internal Color ContentColor { get; set; } = Color.white;
    internal float Born { get; private set; }

    private LoadRef<Sprite> load;
    private GameObject holder;
    private InventoryItemUI copy;

    private ItemIconSource() { }

    internal static ItemIconSource Create(ItemData item, Transform parent, string holderName,
        string diagnosticName, bool requireSpriteReference = false)
    {
        ItemIconSource source = null;
        try
        {
            SoftRef<Sprite> soft = item.GetItemSpriteFromData(ItemUIContext.Default);
            source = new ItemIconSource
            {
                load = soft == null || !soft ? null : SoftRefExtensions.CreateLoadRef(soft, "LastEpoch_Hud", 0),
                Born = Time.unscaledTime
            };
            if (requireSpriteReference && source.load == null && Jewels.JewelVisuals.IconFor(item) == null)
            {
                Main.logger_instance?.Warning(diagnosticName + ": no sprite reference for the item");
                source.Dispose();
                return null;
            }
            source.Content = Jewels.JewelVisuals.IconFor(item);
            try
            {
                source.CopyBackground(item, parent, holderName);
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning(diagnosticName + " background failed: " + ex.Message);
            }
            source.Poll();
            return source;
        }
        catch (Exception ex)
        {
            source?.Dispose();
            Main.logger_instance?.Warning(diagnosticName + " icon failed: " + ex.Message);
            return null;
        }
    }

    private void CopyBackground(ItemData item, Transform parent, string holderName)
    {
        foreach (InventoryItemUI candidate in Object.FindObjectsOfType<InventoryItemUI>())
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate.backgroundImage.IsNullOrDestroyed())
            {
                continue;
            }
            // Record ownership before native calls so partial construction is still disposable.
            holder = new GameObject(holderName);
            holder.AddComponent<RectTransform>();
            holder.transform.SetParent(parent, false);
            holder.SetActive(false);
            copy = Object.Instantiate(candidate.gameObject, holder.transform, false).GetComponent<InventoryItemUI>();
            copy.SetBackgroundSpriteAndColour(copy.backgroundImage, item);
            break;
        }
    }

    internal bool Poll()
    {
        ReadBackground();
        if (Content == null && load != null && load.Status == AssetStatus.Loaded && !load.AssetOrNull.IsNullOrDestroyed())
        {
            Content = load.AssetOrNull;
        }
        return Content != null;
    }

    internal void ReadBackground()
    {
        if (Back != null || copy.IsNullOrDestroyed() || copy.backgroundImage.IsNullOrDestroyed()
            || copy.backgroundImage.sprite == null)
        {
            return;
        }
        Back = copy.backgroundImage.sprite;
        Color colour = copy.backgroundImage.color;
        if (colour.a < 0.2f)
        {
            colour.a = 1f;
        }
        BackColor = colour;
    }

    public void Dispose()
    {
        try
        {
            load?.Dispose();
        }
        catch (Exception)
        {
        }
        load = null;
        if (holder != null)
        {
            Object.Destroy(holder);
        }
        holder = null;
        copy = null;
        Content = null;
        Back = null;
    }
}
