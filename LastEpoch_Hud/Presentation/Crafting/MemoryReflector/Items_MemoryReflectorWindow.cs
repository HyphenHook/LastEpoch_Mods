using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Factions;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// The donor mirror shatters into the Woven Echo. The shards either transfer a memory to the left item or dissolve.
/// </summary>
internal static partial class Items_MemoryReflectorWindow
{
    private const string ForgePath = "GUI/Panel System/Panel Stacks/Left Panel Stack/ForgePanel (Clone)";
    private const string ButtonName = "MemoryReflectorButton";
    private const string RootName = "MemoryReflectorWindow";
    private const float Left = 48, WinW = 670, IconW = 119f, IconH = 174f;
    private const float AY = 142, BY = AY, SlotOffset = 149f;
    private static float CenterX => Left + WinW / 2;

    // ---------------------------------------------------------------- state

    private static GameObject root;
    private static GameObject buttonTemplate, labelTemplate;
    private static TMP_FontAsset headFont, bodyFont;
    private static RectTransform contentRect;
    private static Vector2 contentBase;
    private static ItemData itemA, itemB;
    private static Items_MemoryReflector.Offer offer;
    private static int selectedId = -1;
    private static bool dirty = true;
    private static float nextCheck;
    private static int escapeFrames;
    private static readonly List<GameObject> dynamic = new();
    public static bool IsOpen => root != null && root.activeSelf;
    private static Transform Content => root.transform.Find("Content");

    // ---------------------------------------------------------------- tick, button, open and close

    public static void Tick()
    {
        try
        {
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
                TickHover();
                TickIcons();
                TickRows();
            }
            if (IsOpen && dirty)
            {
                Refresh();
                foreach (GameObject go in effects)
                {
                    if (go != null)
                    {
                        go.transform.SetAsLastSibling(); // effects stay above the rebuilt window
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Memory Reflector window: " + ex.Message);
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
        CraftingWidgets.SetText(button, "Memory Reflector");
        RectTransform rect = button.GetComponent<RectTransform>();
        RectTransform source = template.GetComponent<RectTransform>();
        Transform guide = main.Find("HelpButton");
        RectTransform help = guide != null ? guide.GetComponent<RectTransform>() : null;
        rect.sizeDelta = new Vector2(190, source.sizeDelta.y);
        if (help != null)
        {
            // directly under the Guide button, which hangs from the top right corner
            rect.anchorMin = help.anchorMin;
            rect.anchorMax = help.anchorMax;
            rect.pivot = help.pivot;
            rect.anchoredPosition = new Vector2(help.anchoredPosition.x, help.anchoredPosition.y - help.sizeDelta.y - 6);
        }
        else
        {
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.anchoredPosition = new Vector2(source.anchoredPosition.x, source.anchoredPosition.y - 2 * (source.sizeDelta.y + 6));
        }
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
            Items_AdvancedForgeWindow.Close();
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
            ReturnItems();
            selectedId = -1;
            dirty = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Memory Reflector open failed: " + ex.Message);
        }
    }

    public static void Close()
    {
        if (root == null)
        {
            return;
        }
        CloseTooltip();
        EndAnimationNow();
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i]?.Dispose();
            icons[i] = null;
        }
        ReturnItems();
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
