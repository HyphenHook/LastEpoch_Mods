using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using LastEpoch_Hud.Scripts.Core.Crafting;
using Il2Cpp;
using Il2CppLE.AssetBundles;
using Il2CppLE.AssetManagement;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;

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
        R.StripGameLogic(button);
        R.SetText(button, "Advanced Forge");
        RectTransform rect = button.GetComponent<RectTransform>();
        RectTransform source = template.GetComponent<RectTransform>();
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.sizeDelta = new Vector2(150, source.sizeDelta.y);
        // under the Recombinate button, which is under the Crafting Materials button
        rect.anchoredPosition = new Vector2(source.anchoredPosition.x, source.anchoredPosition.y - 2 * (source.sizeDelta.y + 6));
        R.OnClick(button, OpenFromForge);
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

    private static void ReturnItem()
    {
        GiveBack(item);
        item = null;
        ResetEchoView();
        ResetMemoryView();
    }

    private static void GiveBack(ItemData given)
    {
        ItemContainersManager manager = ItemContainersManager.Instance;
        if (given.IsNullOrDestroyed() || manager.IsNullOrDestroyed())
        {
            return;
        }
        if (!manager.inventory.TryAddItem(given, 1, Context.DEFAULT))
        {
            manager.cursor.TryAddItem(given, 1, Context.DEFAULT);
        }
    }

    // ---------------------------------------------------------------- building the window

    private static bool Build(GameObject forge, Transform parent)
    {
        if (root != null)
        {
            return true;
        }
        Transform main = forge.transform.Find("MainContent");
        if (main == null)
        {
            return false;
        }
        buttonTemplate = main.Find("ViewCraftingMaterialsButton")?.gameObject;
        labelTemplate = main.Find("Header/Title")?.gameObject;
        if (buttonTemplate == null || labelTemplate == null)
        {
            return false;
        }
        headFont = labelTemplate.GetComponent<TextMeshProUGUI>()?.font;
        bodyFont = main.Find("SealedAffixInfoHolder/AffixName")?.GetComponent<TextMeshProUGUI>()?.font ?? headFont;

        root = new GameObject(RootName);
        root.AddComponent<RectTransform>();
        root.AddComponent<UIMouseListener>(); // the pointer is over UI, so clicks do not move the character
        root.transform.SetParent(parent, false);
        R.CopyRect(forge.GetComponent<RectTransform>(), root.GetComponent<RectTransform>());

        var content = new GameObject("Content");
        content.AddComponent<RectTransform>();
        content.transform.SetParent(root.transform, false);
        R.CopyRect(main.GetComponent<RectTransform>(), content.GetComponent<RectTransform>());

        foreach (string piece in new[] { "backgroundTile", "backgroundTile (1)", "Framing", "EterraFrameHeader", "Header" })
        {
            Transform source = main.Find(piece);
            if (source != null)
            {
                GameObject copy = Object.Instantiate(source.gameObject, content.transform, false);
                copy.name = piece;
                R.StripGameLogic(copy);
            }
        }
        Transform title = content.transform.Find("Header/Title");
        if (title != null && title.GetComponent<TextMeshProUGUI>() != null)
        {
            title.GetComponent<TextMeshProUGUI>().text = "Advanced Forge";
        }
        Transform close = content.transform.Find("Header/Close_Button");
        if (close != null)
        {
            R.OnClick(close.gameObject, Close);
        }
        CaptureForgeArt(main);
        AffixCard.Prepare(main);
        root.SetActive(false);
        return true;
    }

    private sealed class EdgeSprite
    {
        public Sprite Sprite;
        public Image.Type Type;
        public Color Color;
        public float Thickness;
    }

    private static readonly EdgeSprite[] edges = new EdgeSprite[4];

    private static void CaptureForgeArt(Transform main)
    {
        string[] names = { "Framing/border-piece-top", "Framing/border-piece-bottom", "Framing/border-piece-left", "Framing/border-piece-right" };
        for (int i = 0; i < 4; i++)
        {
            Image img = main.Find(names[i])?.GetComponent<Image>();
            if (img == null)
            {
                continue;
            }
            Vector2 size = img.rectTransform.sizeDelta;
            float thick = i < 2 ? Mathf.Abs(size.y) : Mathf.Abs(size.x);
            edges[i] = new EdgeSprite { Sprite = img.sprite, Type = img.type, Color = img.color, Thickness = Mathf.Clamp(thick > 0 ? thick : 10, 6, 12) };
        }
    }

    // ---------------------------------------------------------------- small drawing helpers

    private static void ClearDynamic()
    {
        foreach (GameObject go in dynamic)
        {
            if (go != null)
            {
                Object.Destroy(go);
            }
        }
        dynamic.Clear();
        memoryViews.Clear();
        memoryCardVfx.Clear();
        echoCards.Clear();
    }

    private static GameObject Label(string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align, Transform parent = null, List<GameObject> keep = null, bool body = false)
    {
        GameObject go = Object.Instantiate(labelTemplate, parent ?? Content, false);
        R.StripGameLogic(go);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = body ? bodyFont : headFont;
        if (font != null)
        {
            t.font = font;
        }
        // all text of the window is in capitals, in the game's own font
        t.enableAutoSizing = false; // the size given is the size shown
        t.fontStyle = FontStyles.UpperCase;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        go.SetActive(true);
        (keep ?? dynamic).Add(go);
        return go;
    }

    private static void Stretch(RectTransform r, float margin)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(margin, margin);
        r.offsetMax = new Vector2(-margin, -margin);
    }

    // ---------------------------------------------------------------- the item's icon

    private sealed class Icon
    {
        public Sprite Back, Content;
        public Color BackColor = Color.white;
        public LoadRef<Sprite> Load;
        public GameObject Holder;
        public InventoryItemUI Copy;

        public bool Poll()
        {
            if (Back == null && !Copy.IsNullOrDestroyed() && !Copy.backgroundImage.IsNullOrDestroyed() && Copy.backgroundImage.sprite != null)
            {
                Back = Copy.backgroundImage.sprite;
                BackColor = Copy.backgroundImage.color;
                if (BackColor.a < 0.2f)
                {
                    BackColor.a = 1f;
                }
            }
            if (Content == null && Load != null && Load.Status == AssetStatus.Loaded && !Load.AssetOrNull.IsNullOrDestroyed())
            {
                Content = Load.AssetOrNull;
            }
            return Content != null;
        }

        public void Dispose()
        {
            try
            {
                Load?.Dispose();
            }
            catch (Exception)
            {
            }
            if (Holder != null)
            {
                Object.Destroy(Holder);
            }
            Holder = null;
            Copy = null;
            Load = null;
            Content = null;
            Back = null;
        }
    }

    private static Icon icon;
    private static long iconFor;

    private static Icon IconFor(ItemData data)
    {
        long key = (long)data.Pointer;
        if (icon != null && iconFor == key)
        {
            return icon;
        }
        icon?.Dispose();
        icon = BuildIcon(data);
        iconFor = key;
        return icon;
    }

    private static Icon BuildIcon(ItemData data)
    {
        try
        {
            SoftRef<Sprite> soft = data.GetItemSpriteFromData(ItemUIContext.Default);
            LoadRef<Sprite> load = soft == null || !soft ? null : SoftRefExtensions.CreateLoadRef(soft, "LastEpoch_Hud", 0);
            var made = new Icon { Load = load };
            try
            {
                InventoryItemUI source = Object.FindObjectsOfType<InventoryItemUI>()
                    .FirstOrDefault(u => u != null && u.gameObject.activeInHierarchy && !u.backgroundImage.IsNullOrDestroyed());
                if (source != null)
                {
                    var holder = new GameObject("AdvancedForgeBackgroundHolder");
                    holder.AddComponent<RectTransform>();
                    holder.transform.SetParent(root.transform, false);
                    holder.SetActive(false);
                    InventoryItemUI copy = Object.Instantiate(source.gameObject, holder.transform, false).GetComponent<InventoryItemUI>();
                    copy.SetBackgroundSpriteAndColour(copy.backgroundImage, data);
                    made.Holder = holder;
                    made.Copy = copy;
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Advanced Forge background failed: " + ex.Message);
            }
            made.Poll();
            return made;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Advanced Forge icon failed: " + ex.Message);
            return null;
        }
    }

    private static void TickIcon()
    {
        if (icon != null && (icon.Content == null || icon.Back == null))
        {
            bool had = icon.Content != null;
            icon.Poll();
            if (!had && icon.Content != null)
            {
                dirty = true;
            }
        }
    }

    // ---------------------------------------------------------------- the slot while it holds an item

    private sealed class Glimmer
    {
        public RectTransform Rect;
        public Image Img;
        public float Seed, Speed, Size, X0, Sway, SwaySpeed;
    }

    private static readonly List<Glimmer> glimmers = new();
    private static Image slotGlow;

    /// <summary>A violet light behind the item, inside the frame. It is drawn first, so the item is in front of it.</summary>
    private static void AmbientBelow(Transform frame)
    {
        slotGlow = R.MakeImage(frame, "SlotGlow", new Color(0.62f, 0.35f, 1f, 0.3f));
        slotGlow.sprite = OrbArt.Named("glow_violet");
        slotGlow.raycastTarget = false;
        RectTransform r = slotGlow.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(FrameW * 1.25f, FrameH * 1.15f);
        if (Additive() != null)
        {
            slotGlow.material = additive;
        }
    }

    /// <summary>Glimmers that drift up past the item, in front of it.</summary>
    private static void AmbientAbove(Transform frame)
    {
        glimmers.Clear();
        // the glimmers rise and twinkle round the whole frame; where each is at any moment follows the clock, so they carry on
        // unbroken when the window is redrawn
        var rng = new System.Random(7);
        for (int i = 0; i < 18; i++)
        {
            Image img = R.MakeImage(frame, "Glimmer", new Color(1f, 1f, 1f, 0f));
            img.sprite = OrbArt.Named("sparkle_star");
            img.raycastTarget = false;
            if (Additive() != null)
            {
                img.material = additive;
            }
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            float size = 9f + (float)rng.NextDouble() * 15f;
            r.sizeDelta = new Vector2(size, size);
            glimmers.Add(new Glimmer
            {
                Rect = r,
                Img = img,
                Seed = (float)rng.NextDouble(),
                Speed = 0.10f + (float)rng.NextDouble() * 0.12f,
                Size = size,
                X0 = ((float)rng.NextDouble() - 0.5f) * (FrameW + 14f),
                Sway = 3f + (float)rng.NextDouble() * 7f,
                SwaySpeed = 0.8f + (float)rng.NextDouble() * 1.4f,
            });
        }
    }

    private static void TickAmbient()
    {
        float t = Time.unscaledTime;
        bool showGlimmers = !animating || (pendingKey != AdvancedMaterials.EncapsulationOrb && pendingKey != AdvancedMaterials.AttunementOrb);
        if (slotGlow != null)
        {
            slotGlow.color = new Color(0.62f, 0.35f, 1f, 0.26f + 0.16f * Mathf.Sin(t * 2.2f));
        }
        foreach (Glimmer g in glimmers)
        {
            if (g.Rect == null)
            {
                continue;
            }
            float u = (t * g.Speed + g.Seed) % 1f;
            float y = Mathf.Lerp(-FrameH * 0.5f - 6f, FrameH * 0.5f + 6f, u);
            float x = g.X0 + Mathf.Sin(t * g.SwaySpeed + g.Seed * 20f) * g.Sway;
            g.Rect.anchoredPosition = new Vector2(x, y);
            float twinkle = 0.55f + 0.45f * Mathf.Sin(t * 5.5f + g.Seed * 40f);
            float fade = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.8f);
            g.Img.color = new Color(1f, 0.92f, 1f, showGlimmers ? fade * twinkle : 0f);
            g.Rect.localScale = Vector3.one * (0.55f + 0.65f * twinkle);
            g.Rect.localRotation = Quaternion.Euler(0, 0, t * 20f + g.Seed * 90f);
        }
    }

    // ---------------------------------------------------------------- the item slot

    private static RectTransform slotRect; // the whole ornate frame
    private static RectTransform itemRect; // what the frame leaves for the item

    private static void SlotBox(ItemData data)
    {
        Image frame = R.MakeImage(Content, "SlotFrame", Color.white);
        frame.sprite = OrbArt.Named("forge_slot_frame");
        frame.preserveAspect = false;
        RectTransform br = frame.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0, 1);
        br.pivot = new Vector2(0, 1);
        br.anchoredPosition = new Vector2(CenterX - FrameW / 2, -SlotY);
        br.sizeDelta = new Vector2(FrameW, FrameH);
        dynamic.Add(frame.gameObject);
        slotRect = br;
        slotGlow = null;
        glimmers.Clear();
        if (data != null)
        {
            AmbientBelow(frame.transform);
        }

        var holder = new GameObject("Item");
        RectTransform hr = holder.AddComponent<RectTransform>();
        holder.transform.SetParent(frame.transform, false);
        hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.5f);
        hr.pivot = new Vector2(0.5f, 0.5f);
        hr.anchoredPosition = new Vector2(0, FrameH * (0.5f - ItemCentreFromTop));
        hr.sizeDelta = new Vector2(ItemW, ItemH);
        itemRect = hr;
        if (data != null)
        {
            Icon shown = IconFor(data);
            if (shown != null)
            {
                if (shown.Back != null)
                {
                    Image back = R.MakeImage(holder.transform, "ItemBack", shown.BackColor);
                    back.sprite = shown.Back;
                    back.raycastTarget = false;
                    Stretch(back.rectTransform, 2);
                }
                if (shown.Content != null)
                {
                    Image content = R.MakeImage(holder.transform, "ItemIcon", Color.white);
                    content.sprite = shown.Content;
                    content.preserveAspect = true;
                    content.raycastTarget = false;
                    Stretch(content.rectTransform, 10);
                }
            }
        }
        if (data != null)
        {
            AmbientAbove(frame.transform);
        }
        Button button = frame.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener((UnityEngine.Events.UnityAction)ClickSlot);
    }

    private static void ClickSlot()
    {
        if (animating || MemoryClosing)
        {
            return;
        }
        try
        {
            ItemContainersManager manager = ItemContainersManager.Instance;
            if (manager.IsNullOrDestroyed())
            {
                return;
            }
            if (item != null)
            {
                GiveBack(item);
                item = null;
                ResetEchoView();
                ResetMemoryView();
                icon?.Dispose();
                icon = null;
            }
            else
            {
                var content = manager.cursor.GetContent();
                if (content != null && content.Count > 0)
                {
                    ItemContainerEntry entry = content[0];
                    ItemData taken = entry.data;
                    if (!taken.IsNullOrDestroyed() && manager.cursor.TryRemoveItem(entry, 1, Context.DEFAULT))
                    {
                        item = taken;
                    }
                }
            }
            status = "";
            dirty = true;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Advanced Forge slot failed: " + ex.Message);
        }
    }

    // ---------------------------------------------------------------- the orbs and the Forge button

    private sealed class OrbView
    {
        public RectTransform Rect;
        public Image Frame, Art, Glow;
        public TextMeshProUGUI CountText;
        public AdvancedMaterial Material;
        public int Count;
        public bool Have => Count > 0;
    }

    private static readonly List<OrbView> orbViews = new();
    private static string selectedKey;
    private static RectTransform forgeRect;
    private static Image forgeImage;
    private static TextMeshProUGUI forgeLabel;
    private static Image forgeGlow;
    private static float forgeBlend; // 0 at rest, 1 under the pointer
    private static float nextOrbCountRefresh;

    private static TMP_FontAsset countFontCache;

    /// <summary>The font the game draws the count of a stack of items in.</summary>
    private static TMP_FontAsset CountFont()
    {
        if (countFontCache == null)
        {
            try
            {
                InventoryItemUI any = Object.FindObjectsOfType<InventoryItemUI>(true).FirstOrDefault(u => u != null && !u.quantityText.IsNullOrDestroyed());
                countFontCache = any?.quantityText.font;
            }
            catch (Exception)
            {
            }
        }
        return countFontCache;
    }

    private static void OrbRow()
    {
        orbViews.Clear();
        int n = AdvancedMaterials.All.Count;
        float total = n * OrbSize + (n - 1) * 14;
        float x = CenterX - total / 2;
        Label("Orbs", CenterX - 150, OrbY - 26, 300, 20, 15, Gold, TextAlignmentOptions.Center);
        foreach (AdvancedMaterial m in AdvancedMaterials.All)
        {
            int have = AdvancedMaterials.Count(m.Key);
            Image frame = R.MakeImage(Content, "Orb_" + m.Key, Color.white);
            frame.sprite = OrbArt.Named("orb_slot_empty");
            RectTransform br = frame.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0, 1);
            br.pivot = new Vector2(0, 1);
            br.anchoredPosition = new Vector2(x, -OrbY);
            br.sizeDelta = new Vector2(OrbSize, OrbSize);
            dynamic.Add(frame.gameObject);

            Image glow = R.MakeImage(frame.transform, "Glow", new Color(1f, 1f, 1f, 0.5f));
            glow.sprite = OrbArt.Named("glow_violet");
            glow.raycastTarget = false;
            RectTransform gr = glow.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f);
            gr.pivot = new Vector2(0.5f, 0.5f);
            gr.anchoredPosition = Vector2.zero;
            gr.sizeDelta = new Vector2(OrbSize * 0.92f, OrbSize * 0.92f);

            Image art = R.MakeImage(frame.transform, "Art", Color.white);
            art.preserveAspect = true;
            art.raycastTarget = false;
            RectTransform ar = art.rectTransform;
            ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0.5f);
            ar.pivot = new Vector2(0.5f, 0.5f);
            ar.anchoredPosition = Vector2.zero;
            ar.sizeDelta = new Vector2(OrbSize * 0.56f, OrbSize * 0.56f);

            // the count sits at the bottom right of the orb itself (the orb fills the middle 56% of the slot), a little way off it
            float orbRight = OrbSize / 2 + OrbSize * 0.28f, orbBottom = OrbSize / 2 + OrbSize * 0.28f;
            TextMeshProUGUI count = Label(have.ToString("N0"), orbRight + 2 - 30, orbBottom + 1 - 14, 30, 14, 10,
                have > 0 ? Color.white : Dim, TextAlignmentOptions.BottomRight, frame.transform).GetComponent<TextMeshProUGUI>();
            TMP_FontAsset countFont = CountFont();
            if (countFont != null)
            {
                count.font = countFont;
            }
            string key = m.Key;
            Button b = frame.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener((UnityEngine.Events.UnityAction)(() => SelectOrb(key)));
            orbViews.Add(new OrbView { Rect = br, Frame = frame, Art = art, Glow = glow, Material = m, Count = have, CountText = count });
            x += OrbSize + 14;
        }
        if (selectedKey != null && AdvancedMaterials.Count(selectedKey) < 1)
        {
            selectedKey = null;
        }
        ApplyOrbStates();
    }

    private static void TickOrbCounts()
    {
        float now = Time.unscaledTime;
        if (animating || now < nextOrbCountRefresh) return;
        nextOrbCountRefresh = now + 0.2f;
        foreach (OrbView view in orbViews)
        {
            int count = AdvancedMaterials.Count(view.Material.Key);
            if (count == view.Count) continue;
            view.Count = count;
            view.CountText.text = count.ToString("N0");
            view.CountText.color = view.Have ? Color.white : Dim;
            if (!view.Have && selectedKey == view.Material.Key)
            {
                selectedKey = null;
                dirty = true;
            }
        }
    }

    /// <summary>
    /// An orb slot is empty, hovered (a violet ring) or selected (a violet sphere that pulses behind the orb). An orb
    /// that has run out is grey and cannot be selected.
    /// </summary>
    private static void ApplyOrbStates()
    {
        for (int i = 0; i < orbViews.Count; i++)
        {
            OrbView v = orbViews[i];
            if (v.Frame == null)
            {
                continue;
            }
            bool selected = v.Have && selectedKey == v.Material.Key;
            bool hover = v.Have && !selected && hoveredNow == i;
            v.Frame.sprite = OrbArt.Named(selected ? "orb_slot_filled" : hover ? "orb_slot_hover" : "orb_slot_empty");
            v.Art.sprite = OrbArt.Get(v.Material.Key, v.Material.Tint, v.Material.Glow);
            v.Art.color = v.Have ? Color.white : new Color(0.48f, 0.48f, 0.48f);
            v.Glow.enabled = selected;
        }
    }

    private static void PulseOrbs()
    {
        float t = Time.unscaledTime;
        foreach (OrbView v in orbViews)
        {
            if (v.Glow != null && v.Glow.enabled)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 3.2f);
                v.Glow.color = new Color(1f, 1f, 1f, 0.28f + 0.42f * k);
                v.Glow.rectTransform.localScale = Vector3.one * (0.94f + 0.14f * k);
            }
        }
    }

    private static void SelectOrb(string key)
    {
        if (animating || MemoryClosing || AdvancedMaterials.Count(key) < 1)
        {
            return; // an orb that has run out cannot be selected
        }
        selectedKey = selectedKey == key ? null : key;
        if (selectedKey == AdvancedMaterials.AttunementOrb) WarmAttunementVfx();
        status = "";
        dirty = true;
    }

    private static string OrbRestriction(string key) => key switch
    {
        AdvancedMaterials.EncapsulationOrb => Items_Encapsulation.WhyNot(item),
        AdvancedMaterials.ReminiscenceOrb => Items_Reminiscence.WhyNot(item),
        AdvancedMaterials.EchoOrb => Items_EchoOrb.WhyNot(item),
        AdvancedMaterials.ReverieOrb => Items_Reminiscence.ReverieRestriction(item),
        AdvancedMaterials.ChaosOrb => Items_ChaosReroll.WhyNot(item),
        AdvancedMaterials.AttunementOrb => Items_Attunement.WhyNot(item),
        _ => "Select an orb first.",
    };

    private static bool ForgeReady() => selectedKey != null && item != null
        && AdvancedMaterials.Count(selectedKey) > 0 && OrbRestriction(selectedKey).Length == 0;

    private static uint unveiledItemId;
    private static MemoryChoice chosenMemory;
    private static readonly List<AffixCardView> memoryViews = new();
    private static readonly List<(CanvasGroup Face, Image Mist, Image NextMist)> memoryCardVfx = new();
    private static Sprite[] affixRevealFrames, affixVeilFrames;
    private const float MemoryRevealStagger = 0.28f, MemoryRefuseStagger = 0.08f;
    private const float MemoryRevealDuration = 0.70f, MemoryRefuseDuration = 0.72f;
    private static float memoryRevealAt;
    private static float memoryRefuseAt = float.PositiveInfinity;
    private static bool revealRerolledMemories;
    private static bool MemoryClosing => !float.IsPositiveInfinity(memoryRefuseAt);

    private static void ResetMemoryView()
    {
        unveiledItemId = 0;
        chosenMemory = null;
        memoryRefuseAt = float.PositiveInfinity;
        revealRerolledMemories = false;
    }

    private static void ForgeButton(string caption = "Forge", bool memoryAction = false, bool echoAction = false)
    {
        float y = SlotY + FrameH + 4;
        Image img = R.MakeImage(Content, "ForgeButton", Color.white);
        img.sprite = OrbArt.Named(memoryAction || echoAction ? "memory_button_plate" : "forge_button_frame");
        if (echoAction) img.color = new Color(0.61f, 0.95f, 1f);
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(CenterX - ForgeW / 2, -y);
        r.sizeDelta = new Vector2(ForgeW, ForgeH);
        dynamic.Add(img.gameObject);
        forgeRect = r;
        forgeImage = img;
        GameObject label = Label(caption, 0, 0, ForgeW, ForgeH, memoryAction || echoAction ? 14f : 20f, Gold, TextAlignmentOptions.Center, img.transform);
        forgeLabel = label.GetComponent<TextMeshProUGUI>();
        // a violet wash over the frame, which comes up when the pointer is on the button
        Image glow = R.MakeImage(img.transform, "HoverWash", new Color(0.78f, 0.55f, 1f, 0f));
        glow.sprite = img.sprite;
        glow.raycastTarget = false;
        Stretch(glow.rectTransform, 0);
        glow.transform.SetSiblingIndex(0);
        forgeGlow = glow;
        forgeBlend = 0f;
        RectTransform lr = label.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = memoryAction || echoAction ? new Vector2(23f, 13f) : Vector2.zero;
        lr.offsetMax = memoryAction || echoAction ? new Vector2(-23f, -15f) : Vector2.zero;
        if (memoryAction || echoAction)
        {
            forgeLabel.enableWordWrapping = false;
            forgeLabel.enableAutoSizing = true;
            forgeLabel.fontSizeMin = 10f;
            forgeLabel.fontSizeMax = 14f;
        }
        Button b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener((UnityEngine.Events.UnityAction)ForgeClicked);
        TintForge(false);
    }

    private static void RefuseMemoryButton()
    {
        const float width = 158f, height = 48f;
        Image face = R.MakeImage(Content, "RefuseUnveil", Color.white);
        face.sprite = OrbArt.Named("memory_button_plate");
        RectTransform r = face.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(CenterX + ForgeW * 0.5f + 12f, -(SlotY + FrameH + 15f));
        r.sizeDelta = new Vector2(width, height);
        dynamic.Add(face.gameObject);
        Label("Refuse Unveil", 12f, 9f, width - 24f, height - 16f, 11f, Gold, TextAlignmentOptions.Center, face.transform);
        Button button = face.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = new Color(0.78f, 0.74f, 0.83f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.63f, 0.53f, 0.75f);
        button.colors = colors;
        button.onClick.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            if (animating || MemoryClosing) return;
            memoryRefuseAt = Time.unscaledTime;
            status = "";
        }));
    }

    private static void TintForge(bool hover)
    {
        if (forgeImage == null)
        {
            return;
        }
        bool ready = !animating && !MemoryClosing && (Items_Reminiscence.ObscuredOf(item) != null || ForgeReady());
        float target = ready && hover ? 1f : 0f;
        forgeBlend = Mathf.MoveTowards(forgeBlend, target, Time.unscaledDeltaTime * 7f);
        float dim = ready ? 1f : 0.5f;
        forgeImage.color = selectedKey == AdvancedMaterials.EchoOrb && Items_Reminiscence.ObscuredOf(item) == null
            ? new Color(0.61f * dim, 0.95f * dim, dim, ready ? 1f : 0.9f)
            : new Color(dim, dim, dim, ready ? 1f : 0.9f);
        if (forgeGlow != null)
        {
            forgeGlow.color = new Color(0.78f, 0.55f, 1f, 0.34f * forgeBlend);
        }
        if (forgeLabel != null)
        {
            Color rest = ready ? new Color(1f, 0.9f, 0.55f) : new Color(0.6f, 0.58f, 0.55f);
            forgeLabel.color = Color.Lerp(rest, new Color(1f, 1f, 0.92f), forgeBlend);
        }
    }

    private static void ForgeClicked()
    {
        if (animating || MemoryClosing)
        {
            return;
        }
        ObscuredMemory memory = Items_Reminiscence.ObscuredOf(item);
        if (memory != null)
        {
            if (selectedKey == AdvancedMaterials.ReverieOrb)
            {
                if (Items_Reminiscence.ReverieRestriction(item).Length != 0
                    || AdvancedMaterials.Count(AdvancedMaterials.ReverieOrb) < 1)
                {
                    return;
                }
                StartForgeFx(AdvancedMaterials.ReverieOrb);
                return;
            }
            else if (unveiledItemId != item.individualID)
            {
                if (Items_Reminiscence.RevealMemory(item, out status))
                {
                    unveiledItemId = item.individualID;
                    chosenMemory = null;
                    memoryRevealAt = Time.unscaledTime;
                }
                else
                {
                    statusColour = Bad;
                }
            }
            else if (chosenMemory != null)
            {
                bool kept = Items_Reminiscence.Choose(item, memory, chosenMemory, out status);
                statusColour = kept ? Good : Bad;
                if (kept) status = "";
                if (kept)
                {
                    ResetMemoryView();
                    StartMemoryMist();
                }
            }
            dirty = true;
            return;
        }
        string why = OrbRestriction(selectedKey);
        if (why.Length > 0)
        {
            status = why;
            statusColour = Bad;
            dirty = true;
            return;
        }
        if (selectedKey == AdvancedMaterials.EchoOrb)
        {
            EchoClicked();
            return;
        }
        StartForgeFx(selectedKey);
    }

    private static Items_EchoOrb.Echoes shownEchoes;
    private static uint echoItemId;
    private static int selectedEcho = -1;
    private static float echoRevealedAt;
    private static float echoRevealEndsAt;
    private static float echoSelectedAt;
    private static readonly List<ItemData> echoPreviews = new();
    private static readonly List<EchoCardView> echoCards = new();

    private static bool EchoOpen => selectedKey == AdvancedMaterials.EchoOrb && item != null
        && echoItemId == item.individualID && Items_EchoOrb.IsCurrent(item, shownEchoes);

    private static void ResetEchoView()
    {
        shownEchoes = null;
        echoItemId = 0;
        selectedEcho = -1;
        echoPreviews.Clear();
    }

    private static void EchoClicked(bool atImpact = false)
    {
        if (!EchoOpen)
        {
            if (!atImpact)
            {
                StartForgeFx(AdvancedMaterials.EchoOrb);
                return;
            }
            shownEchoes = Items_EchoOrb.Reveal(item, out status);
            if (shownEchoes != null)
            {
                echoItemId = item.individualID;
                selectedEcho = -1;
                echoRevealedAt = Time.unscaledTime;
                echoPreviews.Clear();
                foreach (var rolls in shownEchoes.Options)
                {
                    try
                    {
                        echoPreviews.Add(Items_EchoOrb.Preview(item, rolls));
                    }
                    catch (Exception ex)
                    {
                        Main.logger_instance?.Warning("Echo preview: " + ex.Message);
                        echoPreviews.Add(null);
                    }
                }
            }
            else
            {
                statusColour = Bad;
            }
        }
        else if (selectedEcho >= 0)
        {
            Vector2 origin = echoCards[selectedEcho].Rect.anchoredPosition;
            bool kept = Items_EchoOrb.Keep(item, shownEchoes, selectedEcho, out status);
            statusColour = kept ? Good : Bad;
            if (kept) status = "";
            if (kept)
            {
                PlayEchoCommit(origin);
                ResetEchoView();
                if (AdvancedMaterials.Count(AdvancedMaterials.EchoOrb) == 0)
                {
                    selectedKey = null;
                }
            }
        }
        dirty = true;
    }

    private const TooltipItemManager.SlotType ForgeTooltipSlot = TooltipItemManager.SlotType.CRAFTING;
    private const float EchoCardWidth = 190f, EchoCardHeight = 226f, EchoCardStride = 201f;
    private const float EchoArrivalStagger = 0.13f, EchoCardFadeTime = 0.36f;
    private static float EchoCardsTop => SlotY + FrameH + ForgeH + 39f;

    private sealed class EchoCardView
    {
        public RectTransform Rect;
        public Image Art;
        public CanvasGroup Fade;
        public Image[] Frames;
        public float Hover;
    }

    private static Vector2 EchoCardCenter(int index)
        => new(CenterX + (index - 1) * EchoCardStride, -(EchoCardsTop + EchoCardHeight * 0.5f));

    private static float EchoCardAlpha(int index, float now)
        => Ease(Mathf.Clamp01((now - echoRevealedAt - index * EchoArrivalStagger) / EchoCardFadeTime));

    private static string EchoPercentages(byte[] rolls) =>
        string.Join("  ·  ", rolls.Select(roll => (roll / 255f).ToString("0%")));

    private static string EchoRollSummary(Items_EchoOrb.Rolls rolls)
    {
        var lines = new List<string>();
        void Add(string name, byte[] values)
        {
            if (values.Length == 0) return;
            lines.Add("<color=#90cfcf>" + name + "</color>\n" + EchoPercentages(values));
        }
        Add("Implicit", rolls.Implicits);
        Add("Affix", Items_EchoOrb.AffixPreview(item, rolls));
        return string.Join("\n\n", lines);
    }

    private static void BuildEchoCards()
    {
        Sprite plate = OrbArt.Named("echo_choice_plate");
        for (int i = 0; i < shownEchoes.Options.Count; i++)
        {
            int index = i;
            Image hit = R.MakeImage(Content, "EchoChoice", Color.clear);
            RectTransform rect = hit.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = EchoCardCenter(i);
            rect.sizeDelta = new Vector2(EchoCardWidth, EchoCardHeight);
            dynamic.Add(hit.gameObject);
            var view = new EchoCardView
            {
                Rect = rect,
                Fade = hit.gameObject.AddComponent<CanvasGroup>(),
                Frames = new Image[3],
            };
            view.Fade.alpha = EchoCardAlpha(i, Time.unscaledTime);
            view.Fade.blocksRaycasts = false;
            for (int j = view.Frames.Length - 1; j >= 0; j--)
            {
                view.Frames[j] = EchoCardLayer(hit.transform, "FrameEcho", plate, Color.clear);
            }
            view.Art = EchoCardLayer(hit.transform, "EchoPlate", plate, plate != null ? Color.white : new Color(0.06f, 0.12f, 0.14f));
            Label("Echo " + (i == 0 ? "I" : i == 1 ? "II" : "III"), 14, 25, EchoCardWidth - 28f, 24, 18,
                new Color(0.70f, 0.96f, 0.94f), TextAlignmentOptions.Center, hit.transform);
            GameObject summary = Label(EchoRollSummary(shownEchoes.Options[i]), 16, 57, EchoCardWidth - 32f,
                EchoCardHeight - 83f, 14f, new Color(0.96f, 0.92f, 0.78f), TextAlignmentOptions.Center, hit.transform, body: true);
            TextMeshProUGUI text = summary.GetComponent<TextMeshProUGUI>();
            text.fontStyle = FontStyles.Normal;
            text.enableWordWrapping = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 14f;
            text.raycastTarget = false;

            Button button = hit.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (animating) return;
                bool changed = selectedEcho != index;
                selectedEcho = index;
                echoSelectedAt = Time.unscaledTime;
                status = "";
                if (changed) dirty = true;
            }));
            echoCards.Add(view);
        }
    }

    private static Image EchoCardLayer(Transform parent, string name, Sprite sprite, Color color)
    {
        Image layer = R.MakeImage(parent, name, color);
        layer.sprite = sprite;
        layer.raycastTarget = false;
        Stretch(layer.rectTransform, 0f);
        return layer;
    }

    private static int TickEchoCards(Vector2 mouse, UnityEngine.Camera cam)
    {
        int hovered = -1;
        float now = Time.unscaledTime;
        for (int i = 0; i < echoCards.Count; i++)
        {
            EchoCardView view = echoCards[i];
            view.Fade.alpha = EchoCardAlpha(i, now);
            bool visible = view.Fade.alpha > 0.35f;
            view.Fade.blocksRaycasts = visible && !animating;
            bool hover = view.Fade.blocksRaycasts && RectTransformUtility.RectangleContainsScreenPoint(view.Rect, mouse, cam);
            if (hover) hovered = i;
            float blend = view.Hover = Mathf.MoveTowards(view.Hover, hover ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            bool chosen = selectedEcho == i;
            if (view.Art.sprite != null)
            {
                Color idle = chosen ? new Color(0.91f, 1f, 1f) : new Color(0.80f, 0.87f, 0.89f);
                view.Art.color = Color.Lerp(idle, Color.white, blend);
            }
            for (int j = 0; j < view.Frames.Length; j++)
            {
                float p = (now - echoSelectedAt - (j - 1) * 0.125f) / 0.55f;
                bool ripple = chosen && j > 0 && p >= 0f && p < 1f;
                float expansion = j == 0 ? Mathf.Lerp(2f, 3.5f, blend) : Mathf.Lerp(2f, 12f, Ease(Mathf.Clamp01(p)));
                float alpha = j == 0 ? Mathf.Max(blend * 0.56f, chosen ? 0.72f : 0f)
                    : ripple ? 0.90f * Mathf.Clamp01(p * 12f) * (1f - Ease(p)) : 0f;
                Image frame = view.Frames[j];
                frame.enabled = alpha > 0f && frame.sprite != null;
                if (frame.enabled)
                {
                    Stretch(frame.rectTransform, -expansion);
                    frame.color = new Color(0.38f, 1f, 0.91f, alpha);
                }
            }
        }
        return hovered;
    }
    // ---------------------------------------------------------------- the window

    private static readonly Regex ColourTags = new("</?color[^>]*>|</?link[^>]*>", RegexOptions.Compiled);
    private static readonly Regex LineBreaks = new(@"<br\s*/?>|\r\n|\r|\v|\\n", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static void Refresh()
    {
        dirty = false;
        ClearDynamic();
        CloseTooltips();
        slotRect = null;
        obscuredSand = null;
        itemRect = null;
        forgeImage = null;
        forgeRect = null;

        OrbRow();
        SlotBox(item);
        // the Forge button is there only when an orb is selected and it can be used on the item
        ObscuredMemory memory = Items_Reminiscence.ObscuredOf(item);
        bool unveiled = memory != null && unveiledItemId == item.individualID;
        if (memory != null)
        {
            ShowObscuredSand(Time.unscaledTime, fadeIn: false);
        }
        bool ready = memory == null && selectedKey != AdvancedMaterials.ReverieOrb && ForgeReady();
        if (memory != null && selectedKey == AdvancedMaterials.ReverieOrb)
        {
            ForgeButton("Reroll Memory", memoryAction: true);
        }
        else if (memory != null && !unveiled)
        {
            ForgeButton("Unveil Memory", memoryAction: true);
        }
        else if (unveiled && chosenMemory != null)
        {
            ForgeButton("Keep Memory", memoryAction: true);
        }
        else if (ready)
        {
            if (selectedKey == AdvancedMaterials.AttunementOrb) ForgeButton("Attune");
            else if (selectedKey == AdvancedMaterials.EncapsulationOrb) ForgeButton("Encapsulate");
            else if (selectedKey != AdvancedMaterials.EchoOrb) ForgeButton();
            else if (!EchoOpen) ForgeButton("Reveal Echoes", echoAction: true);
            else if (selectedEcho >= 0) ForgeButton("Keep Echo", echoAction: true);
        }

        float statusY = SlotY + FrameH + ForgeH + 8;
        if (selectedKey == AdvancedMaterials.AttunementOrb && item != null)
        {
            var attunement = Items_Attunement.StateOf(item);
            Label(attunement == null ? "Attunement available\nCorrupts on use" : "Attuned",
                CenterX + ForgeW * 0.5f + 12f, SlotY + FrameH + 20f, 154f, 36f, 11f, Gold, TextAlignmentOptions.MidlineLeft);
        }
        if (selectedKey == AdvancedMaterials.ChaosOrb && item != null)
        {
            Label("Forging Potential  " + item.forgingPotential, CenterX + ForgeW * 0.5f + 12f, SlotY + FrameH + 28f,
                154f, 22f, 13f, Gold, TextAlignmentOptions.MidlineLeft);
        }
        if (unveiled)
        {
            RefuseMemoryButton();
            Label(status.Length == 0 ? "Choose one memory to keep" : status,
                CenterX - 250, statusY, 500, 25, 15, status.Length == 0 ? Gold : statusColour, TextAlignmentOptions.Center);
            MemoryCards(memory, statusY + 44f);
            return;
        }

        if (EchoOpen)
        {
            Label(status.Length == 0 ? "Choose one roll to keep" : status, CenterX - 270f, statusY, 540f,
                24f, 14f, status.Length == 0 ? Gold : statusColour, TextAlignmentOptions.Center);
            BuildEchoCards();
            return;
        }

        string note = status;
        Color noteColour = statusColour;
        if (note.Length == 0 && (memory == null || selectedKey == AdvancedMaterials.EchoOrb) && !ready && selectedKey != null && item != null)
        {
            // an orb is selected and an item is in, but it cannot be used: say why
            note = OrbRestriction(selectedKey);
            noteColour = Bad;
        }
        if (note.Length > 0)
        {
            Label(note, CenterX - 260, statusY, 520, 40, 14, noteColour, TextAlignmentOptions.Top);
        }
        if (item != null)
        {
            AffixPlates(statusY + (note.Length > 0 ? 57f : 21f), memory);
        }
    }

    private static void MemoryCards(ObscuredMemory memory, float top)
    {
        ItemDataUnpacked data = item.TryCast<ItemDataUnpacked>();
        int group = memory.AffixType == (int)AffixList.AffixType.PREFIX ? AffixCard.Prefix : AffixCard.Suffix;
        string side = group == AffixCard.Prefix ? "Prefix memories" : "Suffix memories";
        Label(side, CenterX - 220f, top, 440f, 21f, 13f, AffixCard.GroupColour(group), TextAlignmentOptions.Center);
        top += 27f;
        var choices = memory.Choices.Where(choice => Items_Reminiscence.MatchesMemorySide(memory, choice)).ToList();
        affixRevealFrames ??= OrbArt.Sheet("affix_mist_reveal_sheet", 4, 4);
        affixVeilFrames ??= OrbArt.Sheet("affix_mist_veil_sheet", 4, 4);
        for (int i = 0; i < choices.Count; i++)
        {
            MemoryChoice choice = choices[i];
            ItemAffix preview = Items_Reminiscence.Preview(item, memory, choice);
            string text = string.IsNullOrEmpty(preview.affixName) ? "Affix " + choice.AffixId : preview.affixName;
            if (data != null)
            {
                try
                {
                    string formatted = TooltipItemManager.FormatAffix(data, preview, TooltipMode.DefaultInfo,
                        TooltipItemManager.SlotType.CRAFTING, false, -1, -1, false, false, ValueRangeFormat.None, true, false);
                    if (!string.IsNullOrWhiteSpace(formatted))
                    {
                        text = LineBreaks.Replace(ColourTags.Replace(formatted, ""), "\n");
                    }
                }
                catch (Exception ex)
                {
                    Main.logger_instance?.Warning("Memory preview: " + ex.Message);
                }
            }

            float x = i % 2 == 0 ? CenterX - AffixCard.Width - 6f : CenterX + 6f;
            float y = top + i / 2 * (AffixCard.Height + 8f);
            var card = new AffixCardData { Text = text, Tier = memory.Tier + 1, Group = group, On = choice == chosenMemory };
            AffixCardView view = AffixCard.Build(Content, labelTemplate, bodyFont, card, x, y, dynamic);
            memoryViews.Add(view);
            CanvasGroup face = view.Bg.gameObject.AddComponent<CanvasGroup>();
            Vector2 center = new(x + AffixCard.Width * 0.5f, -y - AffixCard.Height * 0.5f);
            memoryCardVfx.Add((face, MemoryMistLayer(center), MemoryMistLayer(center)));
            Button button = view.Bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (!face.interactable || animating || MemoryClosing) return;
                chosenMemory = choice;
                status = "";
                dirty = true;
            }));
        }
        TickMemoryCards(Time.unscaledTime);
    }

    private static Image MemoryMistLayer(Vector2 center)
    {
        Image mist = R.MakeImage(Content, "MemoryCardMist", Color.clear);
        mist.raycastTarget = false;
        mist.enabled = false;
        RectTransform rect = mist.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = center;
        rect.sizeDelta = new Vector2(AffixCard.Width * 1.12f, AffixCard.Height * 1.66f);
        dynamic.Add(mist.gameObject);
        return mist;
    }

    private static float MemoryCardAlpha(float age) => Ease(Mathf.InverseLerp(0.23f, 0.50f, age));

    private static void DrawMemoryMist(Image layer, Sprite frame, float opacity)
    {
        layer.enabled = frame != null && opacity > 0.002f;
        if (!layer.enabled) return;
        layer.sprite = frame;
        layer.color = new Color(1f, 1f, 1f, opacity);
    }

    private static void TickMemoryCards(float now)
    {
        float coverDuration = MemoryRefuseDuration + Mathf.Max(0, memoryCardVfx.Count - 1) * MemoryRefuseStagger;
        if (MemoryClosing && now - memoryRefuseAt >= coverDuration)
        {
            ResetMemoryView();
            if (selectedKey == AdvancedMaterials.ReverieOrb) selectedKey = null;
            foreach (var card in memoryCardVfx)
            {
                card.Face.alpha = 0f;
                card.Face.blocksRaycasts = false;
                card.Mist.enabled = false;
                card.NextMist.enabled = false;
            }
            dirty = true;
            return;
        }
        float coverAt = revealRerolledMemories && animating ? animStart + EndAt - coverDuration : memoryRefuseAt;
        for (int i = 0; i < memoryCardVfx.Count; i++)
        {
            var card = memoryCardVfx[i];
            float revealAge = now - memoryRevealAt - i * MemoryRevealStagger;
            float faceAlpha = MemoryCardAlpha(revealAge);
            float mistAlpha = Ease(Mathf.InverseLerp(0f, 0.12f, revealAge))
                * (1f - Ease(Mathf.InverseLerp(0.43f, MemoryRevealDuration, revealAge)));
            float mistProgress = Mathf.Clamp01(revealAge / MemoryRevealDuration);
            Sprite[] frames = affixRevealFrames;
            if (now >= coverAt)
            {
                float coverAge = now - coverAt - i * MemoryRefuseStagger;
                float faceAtCover = MemoryCardAlpha(coverAt - memoryRevealAt - i * MemoryRevealStagger);
                faceAlpha = faceAtCover * (1f - Ease(Mathf.InverseLerp(0.16f, 0.40f, coverAge)));
                if (coverAge >= 0f)
                {
                    frames = affixVeilFrames;
                    mistProgress = Mathf.Clamp01(coverAge / MemoryRefuseDuration);
                    mistAlpha = Ease(Mathf.InverseLerp(0f, 0.12f, coverAge))
                        * (1f - Ease(Mathf.InverseLerp(0.48f, MemoryRefuseDuration, coverAge)));
                }
                else
                {
                    mistAlpha *= 1f - Ease(Mathf.InverseLerp(0f, 0.12f, now - coverAt));
                }
            }
            card.Face.alpha = faceAlpha;
            card.Face.interactable = card.Face.blocksRaycasts = !animating && !MemoryClosing && faceAlpha >= 0.99f;
            float sample = mistProgress * (frames.Length - 1);
            int frame = (int)sample;
            int next = Mathf.Min(frame + 1, frames.Length - 1);
            float blend = sample - frame;
            DrawMemoryMist(card.Mist, frames[frame], mistAlpha * (1f - blend));
            DrawMemoryMist(card.NextMist, frames[next], mistAlpha * blend);
        }
    }

    // ---------------------------------------------------------------- the item's affixes

    private sealed class Line
    {
        public string Text;
        public int Tier;
        public bool Encapsulated, Reminded;
        public int Group; // AffixCard.Prefix, Suffix, Sealed, Primordial or Corrupted
    }

    private static List<Line> ReadAffixes()
    {
        var list = new List<Line>();
        ItemDataUnpacked data = item.TryCast<ItemDataUnpacked>();
        foreach (ItemAffix a in item.affixes.ToArray())
        {
            if (a == null)
            {
                continue;
            }
            string text = "Affix " + a.affixId;
            try
            {
                if (data != null)
                {
                    text = TooltipItemManager.FormatAffix(data, a, TooltipMode.DefaultInfo, TooltipItemManager.SlotType.CRAFTING,
                        false, -1, -1, false, false, ValueRangeFormat.None, true, false);
                    text = LineBreaks.Replace(ColourTags.Replace(text ?? "", ""), "\n");
                    text = string.Join("\n", text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
                }
            }
            catch (Exception)
            {
            }
            int group = a.IsSealedCorrupted ? AffixCard.Corrupted : a.IsSealedPrimordial ? AffixCard.Primordial : a.IsSealed ? AffixCard.Sealed
                : a.affixType == AffixList.AffixType.PREFIX ? AffixCard.Prefix : AffixCard.Suffix;
            bool encapsulated = Items_Encapsulate.IsEncapsulated(item, a);
            list.Add(new Line { Text = text, Tier = a.DisplayTier, Group = group, Encapsulated = encapsulated, Reminded = !encapsulated && Items_Reminiscence.IsReminded(item, a) });
        }
        return list;
    }

    private static void Card(Line l, float x, float y)
    {
        var data = new AffixCardData { Text = l.Text, Tier = l.Tier, Group = l.Group, Encapsulated = l.Encapsulated, Reminded = l.Reminded, ReadOnly = true };
        AffixCard.Build(Content, labelTemplate, bodyFont, data, x, y, dynamic);
    }

    /// <summary>
    /// The item's affixes as cards, the same cards as in the other windows: prefixes in a column on the left, suffixes on
    /// the right, and below them the sealed, primordial and corrupted ones.
    /// </summary>
    private static void AffixPlates(float top, ObscuredMemory memory)
    {
        const float gap = 24;
        List<Line> all = ReadAffixes();
        if (memory != null)
        {
            all.Add(new Line { Text = "Obscured Memory", Tier = memory.Tier + 1,
                Group = memory.AffixType == (int)AffixList.AffixType.PREFIX ? AffixCard.Prefix : AffixCard.Suffix });
        }
        float leftX = CenterX - AffixCard.Width - gap / 2, rightX = CenterX + gap / 2;
        float prefixEnd = Column(all.Where(l => l.Group == AffixCard.Prefix).ToList(), leftX, top, AffixCard.Prefix);
        float suffixEnd = Column(all.Where(l => l.Group == AffixCard.Suffix).ToList(), rightX, top, AffixCard.Suffix);
        float y = Math.Max(prefixEnd, suffixEnd);

        List<Line> special = all.Where(l => l.Group >= AffixCard.Sealed).ToList();
        if (special.Count > 0)
        {
            y += 10;
            Label("Sealed, primordial and corrupted", CenterX - 200, y, 400, 20, 13, new Color(0.72f, 0.66f, 0.8f), TextAlignmentOptions.Center);
            y += 24;
            for (int i = 0; i < special.Count; i++)
            {
                Card(special[i], i % 2 == 0 ? leftX : rightX, y);
                if (i % 2 == 1 || i == special.Count - 1)
                {
                    y += AffixCard.Height + AffixCard.Gap;
                }
            }
        }
    }

    /// <summary>One column of cards under a coloured heading. Returns where it ends.</summary>
    private static float Column(List<Line> lines, float x, float y, int group)
    {
        Color colour = AffixCard.GroupColour(group);
        Label(AffixCard.GroupName(group) + "  " + lines.Count, x + 7f, y, AffixCard.Width - 14f, 20f, 13f, colour, TextAlignmentOptions.MidlineLeft);
        y += 26f;
        if (lines.Count == 0)
        {
            Label("Empty", x + 7f, y, AffixCard.Width - 14f, 20f, 12f, Dim, TextAlignmentOptions.MidlineLeft);
            return y + 22f;
        }
        foreach (Line l in lines)
        {
            Card(l, x, y);
            y += AffixCard.Height + AffixCard.Gap;
        }
        return y;
    }

    // ---------------------------------------------------------------- hover: the orbs' own tooltip and the item's

    private static int hoveredNow = -1; // an orb's index, -2 for the item, -1 for nothing
    private const int FirstEchoHover = -3;
    private static bool tooltipOpen;

    private static UnityEngine.Camera PointerCamera()
    {
        Canvas canvas = root.GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private static void CloseTooltips()
    {
        hoveredNow = -1;
        if (tooltipOpen)
        {
            tooltipOpen = false;
            try
            {
                TooltipItemManager.ForceClose();
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>Opens the game's own item tooltip for the item, beside the rectangle.</summary>
    private static void OpenGameTooltip(ItemData data, RectTransform at)
    {
        TooltipItemManager manager = Object.FindObjectOfType<TooltipItemManager>();
        ItemDataUnpacked unpacked = data?.TryCast<ItemDataUnpacked>();
        if (manager != null && unpacked != null)
        {
            manager.OpenTooltip(unpacked, ForgeTooltipSlot, Vector2.zero, at.position, at.gameObject, at.rect.size);
            tooltipOpen = true;
        }
    }

    private static void TickHover()
    {
        Vector2 mouse = Input.mousePosition;
        UnityEngine.Camera cam = PointerCamera();
        int hoveredEcho = TickEchoCards(mouse, cam);
        TickMemoryCards(Time.unscaledTime);
        int target = -1;
        for (int i = 0; target < 0 && i < orbViews.Count; i++)
        {
            if (orbViews[i].Rect != null && RectTransformUtility.RectangleContainsScreenPoint(orbViews[i].Rect, mouse, cam))
            {
                target = i;
                break;
            }
        }
        if (target < 0 && !animating && item != null && itemRect != null && RectTransformUtility.RectangleContainsScreenPoint(itemRect, mouse, cam))
        {
            target = -2;
        }
        if (target == -1 && hoveredEcho >= 0) target = FirstEchoHover - hoveredEcho;
        AffixCard.Tick(memoryViews, animating || MemoryClosing, cam);
        TintForge(forgeRect != null && RectTransformUtility.RectangleContainsScreenPoint(forgeRect, mouse, cam));
        if (target != hoveredNow)
        {
            if (tooltipOpen)
            {
                CloseTooltips();
            }
            hoveredNow = target;
            try
            {
                if (target >= 0)
                {
                    // an orb is shown with the game's own tooltip, through the item it stands in for on the ground
                    OpenGameTooltip(Drop.OrbDrop.TooltipCarrier(orbViews[target].Material.Key), orbViews[target].Rect);
                }
                else if (target == -2)
                {
                    OpenGameTooltip(item, itemRect);
                }
                else if (target <= FirstEchoHover)
                {
                    int index = FirstEchoHover - target;
                    OpenGameTooltip(echoPreviews[index], echoCards[index].Rect);
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Advanced Forge tooltip failed: " + ex.Message);
            }
        }
        ApplyOrbStates();
        PulseOrbs();
    }

    // ---------------------------------------------------------------- the forging effect

    private static readonly List<GameObject> effects = new();
    private static bool animating;
    private static float animStart;
    private static string pendingKey;
    private static bool applied;

    private static Vector2 shakeBase;

    private const float OrbFlyEnd = 0.45f, VfxStart = 0.20f, ShatterAt = 0.80f;
    private const float ImpactAt = 0.98f;
    private static float EndAt => pendingKey == AdvancedMaterials.EchoOrb
        ? Mathf.Max(2.85f, echoRevealEndsAt - animStart) : pendingKey == AdvancedMaterials.AttunementOrb ? 3.65f : 2.48f;

    private static GameObject orbFx;
    private static Image orbFxImage, backGlow, bloom, ringFx;
    private static RectTransform orbFxRect, backGlowRect, bloomRect, ringRect;
    private static Vector2 orbStart;
    private static bool shatterDone;

    private sealed class Spark
    {
        public GameObject Go;
        public RectTransform Rect;
        public Image Img;
        public Vector2 Pos, Vel;
        public float Born, Life;
        public bool Ember;
    }

    private static readonly List<Spark> sparks = new();

    private static Vector2 SlotCentre => new(CenterX, -(SlotY + FrameH * ItemCentreFromTop));
    private static Vector2 OrbPoint => SlotCentre + Vector2.up * 74f;

    private static Material additive;
    private static bool additiveTried;

    /// <summary>An additive material for glows, if the game has a shader for it.</summary>
    internal static Material Additive()
    {
        if (!additiveTried)
        {
            additiveTried = true;
            foreach (string name in new[] { "Legacy Shaders/Particles/Additive", "Particles/Additive", "Mobile/Particles/Additive", "UI/Additive" })
            {
                try
                {
                    Shader shader = Shader.Find(name);
                    if (shader != null && shader.isSupported)
                    {
                        additive = new Material(shader);
                        Main.logger_instance?.Msg("Advanced Forge: additive glow with " + name);
                        break;
                    }
                }
                catch (Exception)
                {
                }
            }
            if (additive == null)
            {
                Main.logger_instance?.Msg("Advanced Forge: no additive shader found, glows use normal blending");
            }
        }
        return additive;
    }

    private static GameObject Effect(string name)
    {
        var go = new GameObject(name);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = Vector2.zero;
        go.transform.SetParent(Content, false);
        effects.Add(go);
        return go;
    }

    private static Image CentredImage(string name, Sprite sprite, float w, float h, out RectTransform rect, bool glow = false)
    {
        GameObject go = Effect(name);
        rect = go.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        Image img = R.MakeImage(go.transform, "Image", Color.white);
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        Stretch(img.rectTransform, 0);
        if (glow && Additive() != null)
        {
            img.material = additive;
        }
        return img;
    }

    private static void StartForgeFx(string key)
    {
        animating = true;
        applied = false;
        shatterDone = false;
        pendingKey = key;
        animStart = Time.unscaledTime;
        echoRevealEndsAt = animStart;
        status = "";
        CloseTooltips();


        // where the orb is lifted from: its own slot
        orbStart = OrbPoint + new Vector2(0, 150);
        foreach (OrbView v in orbViews)
        {
            if (v.Material.Key == key && v.Rect != null)
            {
                orbStart = v.Rect.anchoredPosition + new Vector2(OrbSize / 2, -OrbSize / 2);
            }
        }
        AdvancedMaterial m = AdvancedMaterials.All.Find(x => x.Key == key);

        shakeBase = new Vector2(CenterX - FrameW / 2, -SlotY);

        // a dark violet light behind the orb while it charges
        backGlow = CentredImage("BackGlow", OrbArt.Named("glow_violet"), 300f, 300f, out backGlowRect, glow: true);
        backGlow.color = new Color(0.6f, 0.3f, 1f, 0f);
        backGlowRect.anchoredPosition = OrbPoint;

        orbFxImage = CentredImage("FloatingOrb", OrbArt.Get(key, m.Tint, m.Glow), 104f, 104f, out orbFxRect);
        orbFxRect.anchoredPosition = orbStart;
        orbFx = orbFxRect.gameObject;

    }

    private static float Ease(float u) => u * u * (3f - 2f * u);

    private static readonly List<(Image Image, RectTransform Rect, float Born, int Layer, bool Pale)> memoryMist = new();

    private static void StartMemoryMist(bool pale = false, float delay = 0f)
    {
        Sprite veil = OrbArt.Named("memory_unveil_mist");
        if (veil == null)
        {
            return;
        }
        float now = Time.unscaledTime;
        for (int i = 0; i < 3; i++)
        {
            Image mist = CentredImage("UnveiledMemoryMist", veil, ItemW, ItemH, out RectTransform rect, glow: i == 1);
            mist.color = Color.clear;
            rect.anchoredPosition = SlotCentre;
            memoryMist.Add((mist, rect, now + delay + i * 0.11f, i, pale));
        }
    }

    private static void TickMemoryMist(float now)
    {
        for (int i = memoryMist.Count - 1; i >= 0; i--)
        {
            var mist = memoryMist[i];
            float age = now - mist.Born;
            if (mist.Image == null || age >= 1.25f)
            {
                if (mist.Rect != null)
                {
                    effects.Remove(mist.Rect.gameObject);
                    Object.Destroy(mist.Rect.gameObject);
                }
                memoryMist.RemoveAt(i);
                continue;
            }

            float progress = Mathf.Clamp01(age / 1.25f);
            float opacity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.19f)) * Mathf.Pow(1f - progress, 1.45f);
            float direction = mist.Layer == 1 ? -1f : 1f;
            mist.Rect.anchoredPosition = SlotCentre + new Vector2(direction * progress * 48f, -23f + progress * 70f);
            mist.Rect.sizeDelta = new Vector2(ItemW + progress * 112f, ItemH + progress * 56f);
            mist.Rect.localRotation = Quaternion.Euler(0f, 0f, direction * (15f + progress * 31f));
            mist.Image.color = mist.Pale ? new Color(1f, 1f, 1f, opacity * 0.84f)
                : mist.Layer == 1 ? new Color(0.95f, 0.81f, 1f, opacity * 0.83f)
                : new Color(0.72f, 0.60f, 1f, opacity * 0.68f);
        }
    }

    private static readonly List<(Image Image, RectTransform Rect, Vector2 Source, Vector2 Scatter, float Born, float Spin)> reverieShards = new();

    private static void StartReverieScatter()
    {
        Vector2 source = SlotCentre + Vector2.up * (ItemH * 0.65f);
        foreach (OrbView orb in orbViews)
        {
            if (orb.Material.Key == AdvancedMaterials.ReverieOrb)
            {
                source = orb.Rect.anchoredPosition + new Vector2(OrbSize * 0.5f, -OrbSize * 0.5f);
                break;
            }
        }

        if (animating && pendingKey == AdvancedMaterials.ReverieOrb) source = OrbPoint;
        StartMemoryMist(pale: true, delay: 0.24f);
        StartOrbSeal(AdvancedMaterials.ReverieOrb, 0.63f);
        Sprite sprite = OrbArt.Diamond();
        if (sprite == null) return;

        float now = Time.unscaledTime;
        for (int i = 0; i < 13; i++)
        {
            float angle = i * 2.39996f;
            Vector2 scatter = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.58f) * (40f + i * 4f);
            Image shard = CentredImage("ReverieCrystal", sprite, 11f + i % 3 * 3f, 27f + i % 4 * 6f,
                out RectTransform rect, glow: true);
            rect.anchoredPosition = source;
            shard.color = Color.clear;
            reverieShards.Add((shard, rect, source, scatter, now + i % 5 * 0.044f, (i % 2 == 0 ? 1f : -1f) * (290f + i * 17f)));
        }
    }

    private static void TickReverieScatter(float now)
    {
        for (int i = reverieShards.Count - 1; i >= 0; i--)
        {
            var shard = reverieShards[i];
            float age = now - shard.Born;
            if (shard.Image == null || age >= 0.89f)
            {
                if (shard.Rect != null)
                {
                    effects.Remove(shard.Rect.gameObject);
                    Object.Destroy(shard.Rect.gameObject);
                }
                reverieShards.RemoveAt(i);
                continue;
            }

            float burst = Ease(Mathf.Clamp01(age / 0.24f));
            float pull = Ease(Mathf.InverseLerp(0.24f, 0.82f, age));
            Vector2 arc = shard.Scatter.normalized * (24f * Mathf.Sin(pull * Mathf.PI));
            shard.Rect.anchoredPosition = Vector2.Lerp(shard.Source + shard.Scatter * burst, SlotCentre, pull) + arc;
            shard.Rect.localRotation = Quaternion.Euler(0f, 0f, shard.Spin * age);
            shard.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.26f, pull);
            float alpha = Mathf.Clamp01(age / 0.12f) * (1f - Mathf.InverseLerp(0.77f, 0.89f, age));
            shard.Image.color = new Color(1f, 0.94f, 1f, alpha);
        }
    }

    private static Sprite[] echoPulseFrames;
    private static Sprite echoStrandSprite;
    private const float EchoStrandTravel = 0.62f, EchoStrandTail = 0.22f;
    private static readonly List<(Image Image, RectTransform Rect, Vector2 From, Vector2 To, float Born, float Duration, float Opacity, bool ItemGhost)> echoPulses = new();
    private static readonly List<EchoStrand> echoStrands = new();

    private sealed class EchoStrand
    {
        public GameObject Root;
        public Image[] Segments;
        public Vector2 From, Bend, To;
        public Color Tint;
        public float Born;
    }

    private static void StartEchoPulse(Vector2 from, Vector2 to, float born, float duration, float size, float opacity)
    {
        echoPulseFrames ??= OrbArt.Sheet("echo_pulse_sheet", 4, 4);
        if (echoPulseFrames[0] == null) return;
        Image image = CentredImage("EchoPulse", echoPulseFrames[0], size, size, out RectTransform rect);
        rect.anchoredPosition = from;
        image.color = Color.clear;
        echoPulses.Add((image, rect, from, to, born, duration, opacity, false));
    }

    private static void PlayEchoReveal(float now)
    {
        Sprite itemSprite = icon?.Content;
        for (int i = 0; i < 3; i++)
        {
            float beat = now + 0.19f + i * 0.19f;
            StartEchoPulse(SlotCentre, SlotCentre, beat, 0.56f, 216f + 29f * i, 1f - 0.12f * i);
            if (itemSprite != null)
            {
                Image ghost = CentredImage("EchoItemAfterimage", itemSprite, ItemW - 20f, ItemH - 20f, out RectTransform ghostRect);
                ghostRect.anchoredPosition = SlotCentre;
                ghost.color = Color.clear;
                echoPulses.Add((ghost, ghostRect, SlotCentre, SlotCentre, beat, 0.41f, 0.66f - i * 0.10f, true));
            }
        }
        float launch = now + 1.00f;
        int count = shownEchoes.Options.Count;
        echoRevealedAt = launch + EchoStrandTravel;
        echoRevealEndsAt = echoRevealedAt + (count - 1) * EchoArrivalStagger + EchoCardFadeTime + 0.08f;
        for (int i = 0; i < count; i++)
        {
            float depart = launch + i * EchoArrivalStagger;
            Vector2 destination = EchoCardCenter(i);
            StartEchoStrand(destination, depart, i);
            StartEchoPulse(destination, destination, depart + EchoStrandTravel, 0.42f, 162f, 0.92f);
        }
    }

    private static Sprite EchoStrandSprite()
    {
        if (echoStrandSprite != null) return echoStrandSprite;
        const int width = 32, height = 16;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            float distance = Mathf.Abs((y + 0.5f) * 2f / height - 1f);
            float glow = 0.64f * Mathf.Exp(-distance * distance * 17f)
                + 0.36f * Mathf.Exp(-distance * distance * 3.5f);
            for (int x = 0; x < width; x++)
            {
                float ends = Mathf.Min(1f, Mathf.Min(x, width - 1 - x) / 5f);
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(255f * glow * ends * (1f - distance)));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        echoStrandSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        echoStrandSprite.hideFlags = HideFlags.HideAndDontSave;
        return echoStrandSprite;
    }

    private static void StartEchoStrand(Vector2 destination, float born, int index)
    {
        const int segments = 18;
        GameObject root = Effect("EchoStrand");
        float side = index - 1f;
        var strand = new EchoStrand
        {
            Root = root,
            Segments = new Image[segments],
            From = SlotCentre,
            To = destination,
            Bend = SlotCentre + new Vector2(side == 0f ? 34f : side * 183f, -106f),
            Tint = new Color(0.45f + index * 0.13f, 0.91f + index * 0.04f, 1f - index * 0.055f),
            Born = born,
        };
        Sprite sprite = EchoStrandSprite();
        for (int i = 0; i < segments; i++)
        {
            Image segment = R.MakeImage(root.transform, "Wave", Color.clear);
            segment.sprite = sprite;
            segment.raycastTarget = false;
            strand.Segments[i] = segment;
        }
        echoStrands.Add(strand);
    }

    private static Vector2 EchoStrandPoint(EchoStrand strand, float position, float age)
    {
        Vector2 left = Vector2.Lerp(strand.From, strand.Bend, position);
        Vector2 right = Vector2.Lerp(strand.Bend, strand.To, position);
        float wave = Mathf.Sin(position * 18f - age * 16f) * Mathf.Sin(position * Mathf.PI) * 9f;
        return Vector2.Lerp(left, right, position) + Vector2.right * wave;
    }

    private static void TickEchoStrands(float now)
    {
        for (int i = echoStrands.Count - 1; i >= 0; i--)
        {
            EchoStrand strand = echoStrands[i];
            float age = now - strand.Born;
            if (strand.Root == null || age >= EchoStrandTravel + EchoStrandTail)
            {
                if (strand.Root != null)
                {
                    effects.Remove(strand.Root);
                    Object.Destroy(strand.Root);
                }
                echoStrands.RemoveAt(i);
                continue;
            }
            if (age < 0f) continue;
            float head = Ease(Mathf.Clamp01(age / EchoStrandTravel));
            float tail = Ease(Mathf.Clamp01((age - EchoStrandTail) / EchoStrandTravel));
            float fade = Mathf.InverseLerp(0f, 0.10f, age) *
                (1f - Mathf.InverseLerp(EchoStrandTravel + 0.06f, EchoStrandTravel + EchoStrandTail, age));
            Vector2 previous = EchoStrandPoint(strand, tail, age);
            for (int j = 0; j < strand.Segments.Length; j++)
            {
                float along = (j + 1f) / strand.Segments.Length;
                float position = Mathf.Lerp(tail, head, along);
                Vector2 point = EchoStrandPoint(strand, position, age);
                Vector2 delta = point - previous;
                Image segment = strand.Segments[j];
                float beat = 0.5f + 0.5f * Mathf.Sin(position * 49f - age * 26f);
                segment.rectTransform.anchoredPosition = (point + previous) * 0.5f;
                segment.rectTransform.sizeDelta = new Vector2(delta.magnitude * 1.34f, (5f + 6f * along) * (0.8f + 0.45f * beat));
                segment.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                Color color = Color.Lerp(strand.Tint, Color.white, beat * 0.66f);
                color.a = fade * along * (0.56f + 0.44f * beat);
                segment.color = color;
                previous = point;
            }
        }
    }

    private static void PlayEchoCommit(Vector2 origin)
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < 3; i++)
        {
            StartEchoPulse(origin, SlotCentre, now + i * 0.13f, 0.65f, 139f + 20f * i, 0.95f);
            StartEchoPulse(SlotCentre, SlotCentre, now + 0.46f + i * 0.13f, 0.64f, 211f + 25f * i, 0.94f - 0.14f * i);
        }
    }

    private static void TickEchoPulses(float now)
    {
        for (int i = echoPulses.Count - 1; i >= 0; i--)
        {
            var pulse = echoPulses[i];
            float age = now - pulse.Born;
            if (pulse.Image == null || age >= pulse.Duration)
            {
                if (pulse.Rect != null)
                {
                    effects.Remove(pulse.Rect.gameObject);
                    Object.Destroy(pulse.Rect.gameObject);
                }
                echoPulses.RemoveAt(i);
                continue;
            }
            if (age < 0f) continue;
            float progress = age / pulse.Duration;
            pulse.Rect.anchoredPosition = Vector2.Lerp(pulse.From, pulse.To, Ease(progress));
            if (pulse.ItemGhost)
            {
                pulse.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 1.32f, Ease(progress));
                pulse.Image.color = new Color(0.67f, 1f, 0.94f, Mathf.Sin(progress * Mathf.PI) * (1f - progress) * pulse.Opacity);
                continue;
            }
            int frame = Mathf.Min(echoPulseFrames.Length - 1, (int)(progress * echoPulseFrames.Length));
            pulse.Image.sprite = echoPulseFrames[frame];
            pulse.Image.color = new Color(0.81f, 1f, 0.98f, pulse.Opacity);
        }
    }
    private static Image obscuredSand;
    private const float VeilFadeTime = 0.65f;
    private static float obscuredSandBorn;
    private static Sprite[] chaosEruption;
    private static readonly List<(Image Image, RectTransform Rect, string Key, float Born, int Index, int Emitted)> orbSeals = new();

    private static void ShowObscuredSand(float now, bool fadeIn)
    {
        Sprite mist = OrbArt.Named("orb_memory_sand");
        if (itemRect == null || obscuredSand != null || mist == null) return;
        obscuredSandBorn = fadeIn ? now : now - VeilFadeTime;
        obscuredSand = R.MakeImage(itemRect, "ObscuredSand", ObscuredSandColor(now));
        obscuredSand.sprite = mist;
        obscuredSand.raycastTarget = false;
        Stretch(obscuredSand.rectTransform, 0f);
    }

    private static Color ObscuredSandColor(float now)
    {
        float fade = Ease(Mathf.Clamp01((now - obscuredSandBorn) / VeilFadeTime));
        return new Color(1f, 1f, 1f, fade * (0.47f + 0.035f * Mathf.Sin(now * 1.8f)));
    }

    private static Color OrbTone(string key) => key switch
    {
        AdvancedMaterials.EncapsulationOrb => new Color(0.44f, 0.79f, 1f),
        AdvancedMaterials.ReminiscenceOrb => new Color(0.80f, 0.57f, 0.98f),
        AdvancedMaterials.EchoOrb => new Color(0.38f, 1f, 0.87f),
        AdvancedMaterials.ReverieOrb => new Color(1f, 0.93f, 1f),
        AdvancedMaterials.ChaosOrb => new Color(1f, 0.39f, 0.12f),
        AdvancedMaterials.AttunementOrb => new Color(0.73f, 0.42f, 1f),
        _ => Color.white,
    };

    private static void StartOrbSeal(string key, float delay = 0f)
    {
        string spriteName = key switch
        {
            AdvancedMaterials.EncapsulationOrb => "orb_chain_kunai",
            AdvancedMaterials.ReminiscenceOrb => "orb_memory_sand",
            AdvancedMaterials.ChaosOrb => "orb_chaos_eruption_sheet",
            _ => "ring_violet",
        };
        Sprite sprite = key == AdvancedMaterials.ChaosOrb
            ? (chaosEruption ??= OrbArt.Sheet(spriteName, 4, 4))[0]
            : OrbArt.Named(spriteName);
        if (sprite == null) return;

        int layers = key == AdvancedMaterials.ChaosOrb ? 1 : key == AdvancedMaterials.EncapsulationOrb ? 2 : 3;
        for (int i = 0; i < layers; i++)
        {
            Image image = CentredImage("OrbImpactSeal", sprite, 100f, 100f, out RectTransform rect, glow: key == AdvancedMaterials.ReverieOrb);
            image.color = Color.clear;
            if (key == AdvancedMaterials.ReminiscenceOrb) image.preserveAspect = false;
            rect.anchoredPosition = SlotCentre;
            orbSeals.Add((image, rect, key, Time.unscaledTime + delay + i * 0.044f, i, 0));
        }
    }

    private const float ChainTravelEnd = 0.51f;
    private static Sprite chainEmber;
    private static readonly Color[] ChainSparkColors =
    {
        new(1f, 0.91f, 0.36f),
        new(1f, 0.50f, 0.15f),
        new(0.70f, 0.94f, 0.29f),
    };

    private static Vector2 ChainTip(Vector2 top, Vector2 bottom, float travel)
        => Vector2.Lerp(top, bottom, Mathf.Lerp(0.15f, 1f, Ease(travel)));

    private static Sprite ChainEmber()
    {
        if (chainEmber != null) return chainEmber;
        const int width = 24, height = 8;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[width * height];
        for (int x = 0; x < width; x++)
        {
            float along = (x + 0.5f) / width;
            float intensity = along * (1f - Ease(Mathf.InverseLerp(0.80f, 1f, along)));
            float radius = Mathf.Lerp(0.20f, 0.92f, along);
            for (int y = 0; y < height; y++)
            {
                float across = Mathf.Abs((y + 0.5f) * 2f / height - 1f);
                float edge = 1f - Ease(Mathf.Clamp01(across / radius));
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(intensity * edge * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        chainEmber = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        return chainEmber;
    }

    private static int EmitChainSparks(Vector2 top, Vector2 bottom, float travel, int emitted, int chainIndex)
    {
        const int sparksPerChain = 24;
        int due = Mathf.FloorToInt(travel * sparksPerChain);
        if (emitted >= due) return emitted;

        Vector2 forward = (bottom - top).normalized;
        Vector2 across = new(-forward.y, forward.x);
        // Emit at sampled tip positions to keep the trail continuous through slow frames.
        for (int i = emitted + 1; i <= due; i++)
        {
            Color color = ChainSparkColors[(i + chainIndex) % ChainSparkColors.Length];
            Vector2 tip = ChainTip(top, bottom, (float)i / sparksPerChain);
            Vector2 velocity = -forward * UnityEngine.Random.Range(75f, 190f)
                + across * UnityEngine.Random.Range(-125f, 125f);
            AddSpark(tip, color, UnityEngine.Random.Range(9f, 22f), velocity, UnityEngine.Random.Range(0.13f, 0.27f), ember: true);
        }
        return due;
    }

    private static void TickOrbSeals(float now)
    {
        for (int i = orbSeals.Count - 1; i >= 0; i--)
        {
            var seal = orbSeals[i];
            float elapsed = now - seal.Born;
            float duration = seal.Key switch
            {
                AdvancedMaterials.ReminiscenceOrb => 1.32f,
                AdvancedMaterials.ChaosOrb => 1.08f,
                _ => 0.94f,
            };
            if (seal.Image == null || elapsed >= duration)
            {
                if (seal.Rect != null)
                {
                    effects.Remove(seal.Rect.gameObject);
                    Object.Destroy(seal.Rect.gameObject);
                }
                orbSeals.RemoveAt(i);
                continue;
            }

            if (elapsed < 0f) continue;
            float phase = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Clamp01(elapsed / 0.12f) * (1f - Mathf.InverseLerp(0.66f, 1f, phase));
            if (seal.Key == AdvancedMaterials.EncapsulationOrb)
            {
                float side = seal.Index == 0 ? -1f : 1f;
                float travel = Mathf.InverseLerp(0f, ChainTravelEnd, phase);
                Vector2 top = SlotCentre + new Vector2(side * (ItemW * 0.5f + 6f), ItemH * 0.5f + 10f);
                Vector2 bottom = SlotCentre + new Vector2(-side * (ItemW * 0.5f + 6f), -ItemH * 0.5f - 10f);
                Vector2 tip = ChainTip(top, bottom, travel);
                Vector2 chain = tip - top;
                seal.Rect.anchoredPosition = (top + tip) * 0.5f;
                seal.Rect.sizeDelta = new Vector2(chain.magnitude, 82f);
                seal.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(chain.y, chain.x) * Mathf.Rad2Deg);
                seal.Emitted = EmitChainSparks(top, bottom, travel, seal.Emitted, seal.Index);
                orbSeals[i] = seal;
            }
            else if (seal.Key == AdvancedMaterials.ChaosOrb)
            {
                int frame = Mathf.Clamp((int)(phase * chaosEruption.Length), 0, chaosEruption.Length - 1);
                seal.Image.sprite = chaosEruption[frame];
                seal.Rect.anchoredPosition = SlotCentre + Vector2.up * 26f;
                seal.Rect.sizeDelta = new Vector2(239f, 265f);
            }
            else if (seal.Key == AdvancedMaterials.ReminiscenceOrb)
            {
                float bloom = Ease(Mathf.InverseLerp(0f, 0.34f, phase));
                float drift = Ease(Mathf.InverseLerp(0.29f, 1f, phase));
                float side = seal.Index == 0 ? 0f : seal.Index == 1 ? -1f : 1f;
                seal.Rect.anchoredPosition = SlotCentre + new Vector2(side * (8f * bloom + 59f * drift), -10f + 10f * bloom + 67f * drift);
                seal.Rect.sizeDelta = new Vector2(
                    ItemW * (Mathf.Lerp(0.56f, 1.69f, bloom) + 0.23f * drift),
                    ItemH * (Mathf.Lerp(0.58f, 1.42f, bloom) + 0.19f * drift));
                seal.Rect.localRotation = Quaternion.Euler(0f, 0f, side * (10f + 14f * drift));
                float dissipation = Ease(Mathf.InverseLerp(0.33f, 1f, phase));
                alpha = Ease(Mathf.InverseLerp(0f, 0.21f, phase)) * (1f - dissipation) * (seal.Index == 0 ? 1.15f : 0.51f);
            }
            else
            {
                seal.Rect.anchoredPosition = SlotCentre;
                float size = Mathf.Lerp(45f, 185f, Ease(phase));
                seal.Rect.sizeDelta = new Vector2(size, size * 1.12f);
            }

            Color tint = seal.Key == AdvancedMaterials.ReverieOrb ? OrbTone(seal.Key) : Color.white;
            tint.a = alpha * 0.79f;
            seal.Image.color = tint;
        }
    }

    private static void TickAnimation()
    {
        float now = Time.unscaledTime;
        if (obscuredSand != null)
        {
            obscuredSand.color = ObscuredSandColor(now);
        }
        TickMemoryMist(now);
        TickOrbSeals(now);
        TickOrbFragments(now);
        TickReverieScatter(now);
        TickEchoPulses(now);
        TickEchoStrands(now);
        TickSparks(now);
        TickAttunement(now);
        if (!animating)
        {
            return;
        }
        float t = now - animStart;

        // the orb rises out of its slot to its place over the item, and bobs there until it breaks
        if (orbFx != null)
        {
            float u = Mathf.Clamp01(t / OrbFlyEnd);
            Vector2 pos = Vector2.Lerp(orbStart, OrbPoint, Ease(u));
            if (t >= OrbFlyEnd)
            {
                pos.y += Mathf.Sin((t - OrbFlyEnd) * 5f) * 2.5f;
            }
            orbFxRect.anchoredPosition = pos;
            float charge = Mathf.Clamp01((t - VfxStart) / (ShatterAt - VfxStart));
            orbFxRect.localScale = Vector3.one * (0.7f + 0.3f * Ease(u) + 0.08f * charge);
            float fade = t < ShatterAt ? 1f : Mathf.Clamp01(1f - (t - ShatterAt) / 0.08f);
            orbFxImage.color = new Color(1f, 1f, 1f, fade);
            orbFx.SetActive(fade > 0f);

            // the dark light behind the orb pulses as it charges, and goes at the break
            float pulse = 0.85f + 0.30f * (0.5f + 0.5f * Mathf.Sin(t * 11f));
            float glowAlpha = t < ShatterAt ? 0.55f * Mathf.Clamp01((t - 0.05f) / 0.3f) : 0.55f * Mathf.Clamp01(1f - (t - ShatterAt) / 0.2f);
            Color chargeTone = OrbTone(pendingKey);
            chargeTone.a = glowAlpha;
            backGlow.color = chargeTone;
            backGlowRect.localScale = Vector3.one * pulse;
            backGlowRect.anchoredPosition = pos;
        }

        if (!shatterDone && t >= ShatterAt)
        {
            shatterDone = true;
            ShatterBurst(now);
        }
        if (!applied && t >= ImpactAt)
        {
            applied = true;
            Impact(now);
        }

        // the break: a bloom and a ring spread from the orb
        if (bloom != null)
        {
            float k = Mathf.Clamp01((t - ShatterAt) / 0.16f);
            Color burst = OrbTone(pendingKey);
            burst.a = 0.84f * (1f - k);
            bloom.color = burst;
            bloomRect.localScale = Vector3.one * (0.5f + 1.1f * k);
        }
        if (ringFx != null)
        {
            float k = Mathf.Clamp01((t - ShatterAt) / 0.40f);
            ringFx.color = new Color(1f, 1f, 1f, 0.85f * (1f - k) * (t >= ShatterAt ? 1f : 0f));
            ringRect.localScale = Vector3.one * (0.3f + 1.2f * Ease(k));
        }

        // the slot shudders a little at the break, and again as it takes the essence in
        if (slotRect != null)
        {
            Vector2 shake = Vector2.zero;
            float atBreak = t - ShatterAt, atMerge = t - ImpactAt;
            if (atBreak >= 0 && atBreak < 0.12f)
            {
                shake = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * 2.5f;
            }
            else if (atMerge >= 0 && atMerge < 0.35f)
            {
                shake = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * 3.5f * (1f - atMerge / 0.35f);
            }
            slotRect.anchoredPosition = shakeBase + shake;
        }

        if (t >= EndAt)
        {
            if (revealRerolledMemories) memoryRevealAt = now;
            EndAnimationNow();
            dirty = true;
        }
    }

    private static readonly List<(Image Image, RectTransform Rect, Vector2 Velocity, float Start, float Spin)> orbFragments = new();

    private static void SpawnOrbFragments(float now)
    {
        string asset = pendingKey switch
        {
            AdvancedMaterials.ReminiscenceOrb => "orb_reminiscence",
            AdvancedMaterials.ReverieOrb => "orb_reverie",
            AdvancedMaterials.ChaosOrb => "orb_chaos",
            AdvancedMaterials.AttunementOrb => "orb_attunement",
            _ => "orb_encapsulation",
        };
        Sprite[] pieces = OrbArt.Sheet(asset, 3, 3);
        const float tile = 104f / 3f;
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] == null) continue;
            Vector2 offset = new((i % 3 - 1) * tile, (1 - i / 3) * tile);
            Vector2 velocity = (offset + new Vector2(i - 4f, 8f)).normalized * (115f + i * 14f);
            Image piece = CentredImage("ShatteredOrbPiece", pieces[i], tile, tile, out RectTransform rect);
            rect.anchoredPosition = OrbPoint + offset;
            orbFragments.Add((piece, rect, velocity, now, i % 2 == 0 ? 190f : -225f));
        }
    }

    private static void TickOrbFragments(float now)
    {
        for (int i = orbFragments.Count - 1; i >= 0; i--)
        {
            var fragment = orbFragments[i];
            float age = now - fragment.Start;
            if (fragment.Image == null || age >= 0.48f)
            {
                if (fragment.Rect != null)
                {
                    effects.Remove(fragment.Rect.gameObject);
                    Object.Destroy(fragment.Rect.gameObject);
                }
                orbFragments.RemoveAt(i);
                continue;
            }
            fragment.Rect.anchoredPosition += (fragment.Velocity + Vector2.down * age * 320f) * Time.unscaledDeltaTime;
            fragment.Rect.localRotation = Quaternion.Euler(0f, 0f, fragment.Spin * age);
            fragment.Image.color = new Color(1f, 1f, 1f, 1f - Mathf.InverseLerp(0.10f, 0.48f, age));
        }
    }

    private static void ShatterBurst(float now)
    {
        if (pendingKey == AdvancedMaterials.AttunementOrb)
        {
            SpawnOrbFragments(now);
            StartAttunementBurst(now);
            return;
        }
        if (pendingKey == AdvancedMaterials.EchoOrb)
        {
            StartEchoPulse(OrbPoint, OrbPoint, now, 0.66f, 242f, 1f);
            StartEchoPulse(OrbPoint, OrbPoint, now + 0.14f, 0.64f, 278f, 0.74f);
            return;
        }
        if (pendingKey == AdvancedMaterials.EncapsulationOrb)
        {
            bloom = CentredImage("ChainReleaseFlash", OrbArt.Named("glow_violet"), 126f, 126f, out bloomRect, glow: true);
            bloomRect.anchoredPosition = OrbPoint;
            return;
        }
        SpawnOrbFragments(now);
        bloom = CentredImage("Bloom", OrbArt.Named("glow_violet"), 260f, 260f, out bloomRect, glow: true);
        bloomRect.anchoredPosition = OrbPoint;
        ringFx = CentredImage("Ring", OrbArt.Named("ring_violet"), 230f, 230f, out ringRect, glow: true);
        ringRect.anchoredPosition = OrbPoint;
        for (int i = 0; i < 14; i++)
        {
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            AddSpark(OrbPoint, OrbTone(pendingKey), UnityEngine.Random.Range(2.5f, 5f),
                new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * UnityEngine.Random.Range(80f, 260f), UnityEngine.Random.Range(0.3f, 0.6f));
        }
    }

    private static void Impact(float now)
    {
        if (pendingKey == AdvancedMaterials.AttunementOrb)
        {
            bool attuned = Items_Attunement.UseOn(item, out double multiplier, out status);
            statusColour = attuned ? Good : Bad;
            if (attuned) StartAttunementOutcome(now, multiplier >= 1);
            if (AdvancedMaterials.Count(AdvancedMaterials.AttunementOrb) == 0) selectedKey = null;
            dirty = false;
            return;
        }
        if (pendingKey == AdvancedMaterials.EchoOrb)
        {
            EchoClicked(atImpact: true);
            if (shownEchoes != null)
            {
                PlayEchoReveal(now);
            }
            dirty = shownEchoes != null;
            return;
        }
        if (pendingKey == AdvancedMaterials.ReverieOrb)
        {
            bool rerolled = Items_Reminiscence.RerollMemory(item, out status);
            statusColour = rerolled ? Good : Bad;
            if (rerolled) status = "";
            if (rerolled)
            {
                chosenMemory = null;
                revealRerolledMemories = unveiledItemId == item.individualID;
                StartReverieScatter();
                if (AdvancedMaterials.Count(AdvancedMaterials.ReverieOrb) == 0) selectedKey = null;
            }
            dirty = false;
            return;
        }

        bool ok = false;
        string message = "";
        if (pendingKey == AdvancedMaterials.EncapsulationOrb)
        {
            ok = Items_Encapsulation.UseOn(item, out message);
        }
        else if (pendingKey == AdvancedMaterials.ReminiscenceOrb)
        {
            ok = Items_Reminiscence.UseOn(item, out message);
        }
        else if (pendingKey == AdvancedMaterials.ChaosOrb)
        {
            ok = Items_ChaosReroll.UseOn(item, out message);
        }
        status = ok ? "" : message;
        statusColour = ok ? Good : Bad;
        if (selectedKey != null && AdvancedMaterials.Count(selectedKey) < 1)
        {
            selectedKey = null; // that was the last one
        }
        if (ok) StartOrbSeal(pendingKey);
        dirty = false; // the window is rebuilt when the effect is done, so the item is not redrawn in the middle of it
        if (pendingKey == AdvancedMaterials.EncapsulationOrb) return;
        if (ok && pendingKey == AdvancedMaterials.ReminiscenceOrb)
        {
            ShowObscuredSand(now, fadeIn: true);
            return;
        }
        Color a = OrbTone(pendingKey);
        Color b = !ok ? new Color(1f, 0.55f, 0.4f) : new Color(1f, 0.78f, 0.36f);
        Vector2 at = SlotCentre;
        for (int i = 0; i < (pendingKey == AdvancedMaterials.ChaosOrb ? 34 : 20); i++)
        {
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            float speed = UnityEngine.Random.Range(140f, 520f);
            AddSpark(at, Color.Lerp(a, b, UnityEngine.Random.value), UnityEngine.Random.Range(3f, 7f),
                new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.8f + 0.25f) * speed, UnityEngine.Random.Range(0.35f, 0.9f));
        }
    }

    private static void AddSpark(Vector2 at, Color colour, float size, Vector2 vel, float life, bool ember = false)
    {
        GameObject go = Effect("Spark");
        RectTransform r = go.GetComponent<RectTransform>();
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = ember ? new Vector2(size, UnityEngine.Random.Range(2.5f, 4.5f))
            : new Vector2(size, size * UnityEngine.Random.Range(1f, 2.4f));
        r.anchoredPosition = at;
        r.localRotation = Quaternion.Euler(0, 0, ember ? Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg : UnityEngine.Random.Range(0f, 90f));
        Image img = R.MakeImage(go.transform, "S", colour);
        if (ember) img.sprite = ChainEmber();
        img.raycastTarget = false;
        Stretch(img.rectTransform, 0);
        sparks.Add(new Spark { Go = go, Rect = r, Img = img, Pos = at, Vel = vel, Born = Time.unscaledTime, Life = life, Ember = ember });
    }

    private static void TickSparks(float now)
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark s = sparks[i];
            float age = now - s.Born;
            if (s.Go == null || age >= s.Life)
            {
                if (s.Go != null)
                {
                    effects.Remove(s.Go);
                    Object.Destroy(s.Go);
                }
                sparks.RemoveAt(i);
                continue;
            }
            s.Vel.y -= 520f * dt;
            s.Pos += s.Vel * dt;
            s.Rect.anchoredPosition = s.Pos;
            float remaining = 1f - age / s.Life;
            if (s.Ember)
            {
                s.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(s.Vel.y, s.Vel.x) * Mathf.Rad2Deg);
                s.Rect.localScale = new Vector3(Mathf.Lerp(0.3f, 1f, remaining), 1f, 1f);
            }
            Color c = s.Img.color;
            c.a = remaining;
            s.Img.color = c;
        }
    }

    private static void EndAnimationNow()
    {
        ResetAttunementVfx();
        animating = false;
        revealRerolledMemories = false;
        foreach (GameObject go in effects)
        {
            if (go != null)
            {
                Object.Destroy(go);
            }
        }
        effects.Clear();
        memoryMist.Clear();
        orbSeals.Clear();
        orbFragments.Clear();
        reverieShards.Clear();
        echoPulses.Clear();
        echoStrands.Clear();
        sparks.Clear();
        orbFx = null;
        orbFxImage = null;
        backGlow = null;
        bloom = null;
        ringFx = null;
        if (slotRect != null)
        {
            slotRect.anchoredPosition = shakeBase;
        }
    }
}
