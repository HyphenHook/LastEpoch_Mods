using System.IO;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Data;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Bank;

public partial class Bank_Quad : MonoBehaviour
{

    static bool BindGridImage()
    {
        if (!stash_grid_image.IsNullOrDestroyed())
        {
            return true;
        }
        GameObject gridObject = null;
        if (!stash_panel.IsNullOrDestroyed() && !stash_panel.instance.IsNullOrDestroyed())
        {
            gridObject = Functions.FindDescendant(stash_panel.instance, "grid-img");
        }
        Transform current = stash_item_container_ui.IsNullOrDestroyed()
            ? null
            : stash_item_container_ui.transform;
        for (
            int depth = 0;
            gridObject.IsNullOrDestroyed() && !current.IsNullOrDestroyed() && depth < 8;
            depth++
        )
        {
            if (current.gameObject.name == "grid-img")
            {
                gridObject = current.gameObject;
            }
            else
            {
                gridObject = Functions.FindDescendant(current.gameObject, "grid-img");
            }
            current = current.parent;
        }
        if (gridObject.IsNullOrDestroyed())
        {
            return false;
        }
        stash_grid_image = gridObject.GetComponent<Image>();
        if (stash_grid_image.IsNullOrDestroyed())
        {
            return false;
        }
        if (default_grid.IsNullOrDestroyed())
        {
            default_grid = stash_grid_image.activeSprite;
            default_grid_type = stash_grid_image.type;
            default_grid_preserve_aspect = stash_grid_image.preserveAspect;
            saved_grid_image_settings = true;
            if (!default_grid.IsNullOrDestroyed())
            {
                Object.DontDestroyOnLoad(default_grid);
            }
        }
        return true;
    }

    static bool TryGridPixels(out float width, out float height)
    {
        width = 0f;
        height = 0f;
        BindGridImage();
        if (!stash_grid_image.IsNullOrDestroyed())
        {
            Rect grid = stash_grid_image.rectTransform.rect;
            width = Mathf.Abs(grid.width);
            height = Mathf.Abs(grid.height);
        }
        if (
            ((width < 10f) || (height < 10f))
            && !stash_item_container_ui.IsNullOrDestroyed()
            && !stash_item_container_ui.rectT.IsNullOrDestroyed()
        )
        {
            Rect panel = stash_item_container_ui.rectT.rect;
            width = Mathf.Abs(panel.width);
            height = Mathf.Abs(panel.height);
        }
        return (width >= 10f) && (height >= 10f);
    }

    static ItemContainer ShownContainer()
    {
        if (stash_item_container.IsNullOrDestroyed())
        {
            return null;
        }
        ItemContainer shown = stash_item_container.GetActiveItemContainer();
        if (!shown.IsNullOrDestroyed())
        {
            return shown;
        }
        int active = stash_item_container.CurrentlyActiveTab;
        if ((active < 0) || (active >= stash_item_container.containers.Count))
        {
            return null;
        }
        return stash_item_container.containers[active];
    }

    static bool ActiveTabIsQuad()
    {
        if (stash_item_container.IsNullOrDestroyed())
        {
            return false;
        }
        return Get.IsQuadStash(stash_item_container.CurrentlyActiveTab);
    }

    static Vector2 QuadSlotSize(float width, float height)
    {
        // Use the visible stash grid rectangle. Native slot padding is not part of this
        // coordinate system and moves quad items outside the panel.
        return new Vector2(width / quad_size.x, height / quad_size.y);
    }

    static bool RefreshActiveGrid()
    {
        if (stash_item_container.IsNullOrDestroyed())
        {
            return false;
        }
        if (stash_item_container_ui.IsNullOrDestroyed())
        {
            stash_item_container_ui = UnityEngine.Object.FindObjectOfType<StashItemContainerUI>();
        }
        if (stash_item_container_ui.IsNullOrDestroyed())
        {
            return false;
        }
        if (!TryGridPixels(out float width, out float height))
        {
            return false;
        }

        SyncEveryTab();
        bool quad = ActiveTabIsQuad();
        if (quad)
        {
            Vector2 slot = QuadSlotSize(width, height);
            stash_item_container_ui.slotSize = slot;
            ResizeVisibleIcons(slot);
            icons_use_quad_size = true;
        }
        else if (icons_use_quad_size)
        {
            RestoreNativeIcons();
        }
        EnsureQuadSprite();
        EnsureOverlay();
        if (!quad_overlay.IsNullOrDestroyed())
        {
            quad_overlay.gameObject.SetActive(quad);
            if (quad && !quad_grid.IsNullOrDestroyed())
            {
                quad_overlay.sprite = quad_grid;
                quad_overlay.color = Color.white;
            }
        }
        if (!stash_grid_image.IsNullOrDestroyed())
        {
            if (default_grid.IsNullOrDestroyed())
            {
                default_grid = stash_grid_image.sprite;
            }
            if (!default_grid.IsNullOrDestroyed())
            {
                stash_grid_image.sprite = default_grid;
                if (saved_grid_image_settings)
                {
                    stash_grid_image.type = default_grid_type;
                    stash_grid_image.preserveAspect = default_grid_preserve_aspect;
                }
            }
            stash_grid_image.enabled = !quad;
        }
        stash_item_container_ui.ReDrawWholeContainer();
        return true;
    }

    static void EnsureOverlay()
    {
        if (stash_grid_image.IsNullOrDestroyed())
        {
            return;
        }
        RectTransform grid_rect = stash_grid_image.rectTransform;
        if (grid_rect.IsNullOrDestroyed())
        {
            return;
        }
        if (quad_overlay.IsNullOrDestroyed())
        {
            Transform existing = grid_rect.Find("quad_grid_overlay");
            GameObject overlay_obj = existing.IsNullOrDestroyed()
                ? new GameObject("quad_grid_overlay")
                : existing.gameObject;
            if (existing.IsNullOrDestroyed())
            {
                overlay_obj.transform.SetParent(grid_rect, false);
            }
            quad_overlay = overlay_obj.GetComponent<Image>();
            if (quad_overlay.IsNullOrDestroyed())
            {
                quad_overlay = overlay_obj.AddComponent<Image>();
            }
            quad_overlay.raycastTarget = false;
            quad_overlay.type = Image.Type.Simple;
            quad_overlay.preserveAspect = false;
        }
        RectTransform overlay_rect = quad_overlay.rectTransform;
        if (overlay_rect.parent != grid_rect)
        {
            overlay_rect.SetParent(grid_rect, false);
        }
        overlay_rect.anchorMin = Vector2.zero;
        overlay_rect.anchorMax = Vector2.one;
        overlay_rect.pivot = new Vector2(0.5f, 0.5f);
        overlay_rect.anchoredPosition = Vector2.zero;
        overlay_rect.sizeDelta = Vector2.zero;
        overlay_rect.offsetMin = Vector2.zero;
        overlay_rect.offsetMax = Vector2.zero;
        overlay_rect.localRotation = Quaternion.identity;
        overlay_rect.localScale = Vector3.one;
    }

    static void RestoreNativeIcons()
    {
        icons_use_quad_size = false;
        if (stash_item_container_ui.IsNullOrDestroyed())
        {
            return;
        }
        // Redraw first so the game reports its own cell size for a normal tab.
        stash_item_container_ui.ReDrawWholeContainer();
        if (!have_native_slot)
        {
            return;
        }
        stash_item_container_ui.slotSize = native_slot_size;
        ResizeVisibleIcons(native_slot_size);
    }

    static void ResizeVisibleIcons(Vector2 slot)
    {
        if (stash_item_container_ui.IsNullOrDestroyed())
        {
            return;
        }
        InventoryItemUI[] icons = stash_item_container_ui.GetComponentsInChildren<InventoryItemUI>(
            false
        );
        for (int i = 0; i < icons.Length; i++)
        {
            InventoryItemUI icon = icons[i];
            if (icon.IsNullOrDestroyed())
            {
                continue;
            }
            if ((icon.slotSize - slot).sqrMagnitude < 0.01f)
            {
                continue;
            }
            icon.slotSize = slot;
            ItemContainerEntry entry = icon.EntryRef;
            if (!entry.IsNullOrDestroyed())
            {
                icon.SetPosition(slot, entry);
            }
        }
    }

    // Runs after the game's own UI update, which otherwise puts the 12x17 grid back.
    static void BeforeRender()
    {
        if (
            stash_panel.IsNullOrDestroyed()
            || !stash_panel.isOpen
            || stash_item_container.IsNullOrDestroyed()
        )
        {
            return;
        }
        if (stash_grid_image.IsNullOrDestroyed() || stash_item_container_ui.IsNullOrDestroyed())
        {
            return;
        }
        bool quad = ActiveTabIsQuad();
        if (quad)
        {
            EnsureQuadSprite();
            EnsureOverlay();
        }
        if (!quad_overlay.IsNullOrDestroyed())
        {
            quad_overlay.gameObject.SetActive(quad);
            quad_overlay.enabled = quad;
            if (quad && !quad_grid.IsNullOrDestroyed())
            {
                quad_overlay.sprite = quad_grid;
                quad_overlay.color = Color.white;
            }
        }
        if (!default_grid.IsNullOrDestroyed())
        {
            stash_grid_image.sprite = default_grid;
        }
        if (saved_grid_image_settings)
        {
            stash_grid_image.type = default_grid_type;
            stash_grid_image.preserveAspect = default_grid_preserve_aspect;
        }
        stash_grid_image.enabled = !quad;
    }

    static void EnsureQuadSprite()
    {
        if (!quad_grid.IsNullOrDestroyed())
        {
            return;
        }
        if (!Hud_Manager.asset_bundle.IsNullOrDestroyed())
        {
            foreach (string name in Hud_Manager.asset_bundle.GetAllAssetNames())
            {
                if (
                    (!name.Contains("/quadstash/"))
                    || (!name.Contains("quad_grid"))
                    || (!Functions.Check_Texture(name))
                )
                {
                    continue;
                }
                Texture2D texture = Hud_Manager.asset_bundle.LoadAsset(name).TryCast<Texture2D>();
                if (texture.IsNullOrDestroyed())
                {
                    continue;
                }
                quad_grid = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f)
                );
                UnityEngine.Object.DontDestroyOnLoad(quad_grid);
                return;
            }
        }
        quad_grid = BuildGridSprite(quad_size.x, quad_size.y);
        UnityEngine.Object.DontDestroyOnLoad(quad_grid);
    }

    static Sprite BuildGridSprite(int columns, int rows)
    {
        const int cell = 16;
        int width = columns * cell;
        int height = rows * cell;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[width * height];
        Color32 fill = new Color32(18, 16, 14, 210);
        Color32 line = new Color32(166, 124, 62, 255);
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = fill;
        }
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                int left = x * cell;
                int bottom = y * cell;
                for (int i = 0; i < cell; i++)
                {
                    pixels[(bottom * width) + left + i] = line;
                    pixels[((bottom + cell - 1) * width) + left + i] = line;
                    pixels[((bottom + i) * width) + left] = line;
                    pixels[((bottom + i) * width) + left + cell - 1] = line;
                }
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), cell);
    }
}
