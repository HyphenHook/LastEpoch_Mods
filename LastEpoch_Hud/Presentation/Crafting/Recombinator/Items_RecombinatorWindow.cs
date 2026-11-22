using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.Mods.Character;
using LastEpoch_Hud.Scripts.ModUI.ForceDrop;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// The recombinator window. It is opened by a button on the forge panel, which closes the forge. The frame, header
/// and buttons are copies of the forge's own, so it looks like the rest of the game. Two slots take the item that is
/// held on the cursor (click a slot while holding an item); clicking a filled slot gives the item back. Items still
/// in a slot when the window closes go back into the inventory.
/// </summary>
internal static partial class Items_RecombinatorWindow
{
    private const string ButtonName = "RecombinateButton";
    private const string RootName = "RecombinatorWindow";
    private static GameObject root;
    private static readonly ItemData[] slots = new ItemData[2];
    private static readonly HashSet<string> selected = new();
    private static bool deterministic = true;
    private static bool primordial; // spend ancient bones for a better chance (deterministic only)
    private static Items_Recombinator.Offer offer;
    private static string message = "";
    private static bool dirty = true;
    private static float nextCheck;
    private static float nextRefresh;

    // The result waits in the bottom slot until you take it.
    private static ItemData resultItem;
    private static bool resultRevealed;
    private static float revealAt;
    private static int escapeFrames;
    public static bool IsOpen => root != null && root.activeSelf;

    // ---------------------------------------------------------------- forge button

    /// <summary>Called every frame: adds the forge button once the forge exists, and keeps the window up to date.</summary>
    public static void Tick()
    {
        try
        {
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + 0.5f;
                EnsureForgeButton();
                // The window can only stay open from the forge's own button: if the scene is left it is closed.
                if (IsOpen && Refs_Manager.player_actor.IsNullOrDestroyed())
                {
                    Close();
                }
            }
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                escapeFrames = 3; // the game may have opened its pause menu on the same key; put it away
            }
            if (escapeFrames > 0)
            {
                escapeFrames--;
                try
                {
                    GamePauseMenu.Resume();
                }
                catch (Exception)
                {
                }
            }
            if (IsOpen)
            {
                TickAnimations();
                RecombKit.Preload();
                TickHover();
                TickIcons();
                TickRows();
            }
            if (IsOpen && dirty)
            {
                nextRefresh = Time.unscaledTime + 0.4f;
                Refresh();
                foreach (Ghost g in ghosts)
                {
                    if (g.Go != null)
                    {
                        g.Go.transform.SetAsLastSibling(); // embers stay above the rebuilt window
                    }
                }
                foreach (Piece pc in pieces)
                {
                    if (pc.Go != null)
                    {
                        pc.Go.transform.SetAsLastSibling();
                    }
                }
                if (pendingFlash)
                {
                    pendingFlash = false;
                    if (lastFailed)
                    {
                        SpawnCrack();
                    }
                    else
                    {
                        BurstResultEmbers();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Recombinator window: " + ex.Message);
        }
    }

    private static void EnsureForgeButton()
    {
        GameObject forge = GameObject.Find(ForgePath);
        if (forge == null)
        {
            return;
        }
        Transform main = forge.transform.Find("MainContent");
        if (main == null || main.Find(ButtonName) != null)
        {
            return;
        }
        Transform template = main.Find("ViewCraftingMaterialsButton");
        if (template == null)
        {
            return;
        }
        GameObject button = Object.Instantiate(template.gameObject, main, false);
        button.name = ButtonName;
        CraftingWidgets.StripGameLogic(button);
        CraftingWidgets.SetText(button, "Recombinate");
        RectTransform rect = button.GetComponent<RectTransform>();
        RectTransform source = template.GetComponent<RectTransform>();
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.sizeDelta = new Vector2(150, source.sizeDelta.y);
        // directly under the Crafting Materials button
        rect.anchoredPosition = new Vector2(source.anchoredPosition.x, source.anchoredPosition.y - source.sizeDelta.y - 6);
        CraftingWidgets.OnClick(button, OpenFromForge);
        button.SetActive(true);
    }

    // ---------------------------------------------------------------- open / close

    private static void OpenFromForge()
    {
        try
        {
            GameObject forge = GameObject.Find(ForgePath);
            if (forge == null)
            {
                return;
            }
            Items_MemoryReflectorWindow.Close();
            Items_AdvancedForgeWindow.Close();
            Transform parent = forge.transform.parent;
            if (!Build(forge, parent))
            {
                return;
            }
            // Close the forge the way the game does, then show the window in its place.
            try
            {
                UIBase.instance.closeAllCraftingUIs();
            }
            catch (Exception)
            {
                forge.SetActive(false);
            }
            ReturnItems();
            selected.Clear();
            offer = null;
            message = "";
            dirty = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Recombinator open failed: " + ex.Message);
        }
    }

    public static void Close()
    {
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i]?.Dispose();
            icons[i] = null;
        }
        CloseTooltip();
        ClearGhosts();
        pouring = false;
        RecombKit.Stop();
        pendingFlash = false;
        Array.Clear(pourItems, 0, 2);
        ReturnItems();
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    /// <summary>F is the forge's key: with the recombinator open, it closes the window instead of reopening the forge.</summary>
    [HarmonyPatch(typeof(UIBase), "CraftingKeyDown")]
    internal static class CraftingKey
    {
        static bool Prefix()
        {
            if (!IsOpen)
            {
                return true;
            }
            Close();
            return false;
        }
    }
}
