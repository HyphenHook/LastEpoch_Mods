using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LastEpoch_Hud.Scripts.Core.Crafting;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// The Advanced Forge, opened from a button on the forge. One slot takes an item; a row of orbs above it lists the
/// advanced crafting materials. Clicking an orb brings a hammer down on the item and applies the orb.
/// </summary>
internal static partial class Items_AdvancedForgeWindow
{
    private const string ForgePath = "GUI/Panel System/Panel Stacks/Left Panel Stack/ForgePanel (Clone)";
    private const string ButtonName = "AdvancedForgeButton";
    private const string RootName = "AdvancedForgeWindow";
    private const float Left = 48, WinW = 670, OrbSize = 92;
    private const float FrameW = 190, FrameH = 256; // the whole ornate slot frame
    private const float ItemW = 102, ItemH = 175; // the dark panel inside it, which is the item's box
    private const float ItemCentreFromTop = 0.5f; // the panel's middle, as a share of the frame's height
    private const float ForgeW = 210, ForgeH = 70; // the Forge button
    private const float OrbY = 100, SlotY = 206;
    private static float CenterX => Left + WinW / 2;

    // ---------------------------------------------------------------- state

    private static GameObject root;
    private static GameObject buttonTemplate, labelTemplate;
    private static TMP_FontAsset headFont, bodyFont;
    private static ItemData item;
    private static bool dirty = true;
    private static float nextCheck;
    private static int escapeFrames;
    private static string status = "";
    private static Color statusColour = Color.white;
    private static readonly List<GameObject> dynamic = new();
    private static readonly Color Gold = new(0.93f, 0.78f, 0.38f);
    private static readonly Color Dim = new(0.65f, 0.62f, 0.58f);
    private static readonly Color Good = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Bad = new(0.9f, 0.4f, 0.35f);
    public static bool IsOpen => root != null && root.activeSelf;
    private static Transform Content => root.transform.Find("Content");

    // ---------------------------------------------------------------- tick, button, open and close

    internal static ItemData InspectorItem => IsOpen ? item : null;

    public static void Tick()
    {
        try
        {
            WarmAttunementVfx();
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + 0.5f;
                EnsureForgeButton();
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
                TickAnimation();
                TickOrbCounts();
                TickAmbient();
                TickHover();
                TickIcon();
            }
            if (IsOpen && dirty)
            {
                Refresh();
                foreach (GameObject go in effects)
                {
                    if (go != null)
                    {
                        go.transform.SetAsLastSibling();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Advanced Forge window: " + ex.Message);
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
        CraftingWidgets.SetText(button, "Advanced Forge");
        RectTransform rect = button.GetComponent<RectTransform>();
        RectTransform source = template.GetComponent<RectTransform>();
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.sizeDelta = new Vector2(150, source.sizeDelta.y);
        // under the Recombinate button, which is under the Crafting Materials button
        rect.anchoredPosition = new Vector2(source.anchoredPosition.x, source.anchoredPosition.y - 2 * (source.sizeDelta.y + 6));
        CraftingWidgets.OnClick(button, OpenFromForge);
        button.SetActive(true);
    }

    private static void OpenFromForge()
    {
        try
        {
            GameObject forge = GameObject.Find(ForgePath);
            if (forge == null)
            {
                return;
            }
            Items_RecombinatorWindow.Close();
            Items_MemoryReflectorWindow.Close();
            if (!Build(forge, forge.transform.parent))
            {
                return;
            }
            try
            {
                UIBase.instance.closeAllCraftingUIs();
            }
            catch (Exception)
            {
                forge.SetActive(false);
            }
            ReturnItem();
            status = "";
            dirty = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Advanced Forge open failed: " + ex.Message);
        }
    }

    public static void Close()
    {
        if (root == null)
        {
            return;
        }
        CloseTooltips();
        EndAnimationNow();
        icon?.Dispose();
        icon = null;
        ReturnItem();
        root.SetActive(false);
    }

    /// <summary>F is the forge's key: with this window open it closes the window instead of reopening the forge.</summary>
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
