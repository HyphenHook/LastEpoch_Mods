using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
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
internal static class Items_AdvancedForgeWindow
{
    private const string ForgePath = "GUI/Panel System/Panel Stacks/Left Panel Stack/ForgePanel (Clone)";
    private const string ButtonName = "AdvancedForgeButton";
    private const string RootName = "AdvancedForgeWindow";

    private const float Left = 48, WinW = 670, IconW = 140, IconH = 196, OrbSize = 76;
    private const float OrbY = 96, SlotY = 220;
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
    }

    private static GameObject Label(string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align, Transform parent = null, List<GameObject> keep = null, bool caps = true)
    {
        GameObject go = Object.Instantiate(labelTemplate, parent ?? Content, false);
        R.StripGameLogic(go);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = size >= 18 ? headFont : bodyFont;
        if (font != null)
        {
            t.font = font;
        }
        // short labels are in capitals, as the game's own are; wording and sentences are not
        t.fontStyle = caps && text.Length <= 28 && !text.Contains("\n") ? FontStyles.UpperCase : FontStyles.Normal;
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

    // ---------------------------------------------------------------- the item slot

    private static RectTransform slotRect;

    private static void SlotBox(ItemData data, float x, float y)
    {
        Image box = R.MakeImage(Content, "Slot", new Color(0.05f, 0.04f, 0.035f, 0.97f));
        RectTransform br = box.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0, 1);
        br.pivot = new Vector2(0, 1);
        br.anchoredPosition = new Vector2(x, -y);
        br.sizeDelta = new Vector2(IconW, IconH);
        dynamic.Add(box.gameObject);
        slotRect = br;
        if (data != null)
        {
            Icon shown = IconFor(data);
            if (shown == null || shown.Back == null)
            {
                box.color = Color.Lerp(new Color(0.05f, 0.04f, 0.035f, 0.97f), R.ItemColor(data), 0.2f);
            }
            if (shown != null)
            {
                if (shown.Back != null)
                {
                    Image back = R.MakeImage(box.transform, "ItemBack", shown.BackColor);
                    back.sprite = shown.Back;
                    back.raycastTarget = false;
                    Stretch(back.rectTransform, 4);
                }
                if (shown.Content != null)
                {
                    Image content = R.MakeImage(box.transform, "ItemIcon", Color.white);
                    content.sprite = shown.Content;
                    content.preserveAspect = true;
                    content.raycastTarget = false;
                    Stretch(content.rectTransform, 14);
                }
            }
        }
        SlotFrame(box, data != null ? R.ItemColor(data) : (Color?)null);
        Button button = box.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener((UnityEngine.Events.UnityAction)ClickSlot);
    }

    /// <summary>The forge's own frame pieces round a slot, with a thin line of the item's rarity colour just inside.</summary>
    private static void SlotFrame(Image box, Color? rarity)
    {
        float th = 8f;
        for (int side = 0; side < 4; side++)
        {
            EdgeSprite e = edges[side];
            if (e == null || e.Sprite == null)
            {
                continue;
            }
            th = Mathf.Min(e.Thickness, 12f);
            Image img = R.MakeImage(box.transform, "Border", e.Color);
            img.sprite = e.Sprite;
            img.type = e.Type;
            img.raycastTarget = false;
            RectTransform r = img.rectTransform;
            switch (side)
            {
                case 0: r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(0, th); break;
                case 1: r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(0.5f, 0); r.sizeDelta = new Vector2(0, th); break;
                case 2: r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 0.5f); r.sizeDelta = new Vector2(th, 0); break;
                default: r.anchorMin = new Vector2(1, 0); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 0.5f); r.sizeDelta = new Vector2(th, 0); break;
            }
            r.anchoredPosition = Vector2.zero;
        }
        Color line = rarity ?? new Color(0.5f, 0.4f, 0.24f);
        line.a = 0.9f;
        Transform ft = box.transform;
        R.Part(ft, "RarityTop", line, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -th), new Vector2(-2 * th, 2));
        R.Part(ft, "RarityBottom", line, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, th), new Vector2(-2 * th, 2));
        R.Part(ft, "RarityLeft", line, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(th, 0), new Vector2(2, -2 * th));
        R.Part(ft, "RarityRight", line, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-th, 0), new Vector2(2, -2 * th));
    }

    private static void ClickSlot()
    {
        if (animating)
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

    // ---------------------------------------------------------------- the orbs

    private static readonly List<(RectTransform Rect, AdvancedMaterial Material)> orbRects = new();

    private static void OrbRow()
    {
        orbRects.Clear();
        int n = AdvancedMaterials.All.Count;
        float total = n * OrbSize + (n - 1) * 16;
        float x = CenterX - total / 2;
        Label("Orbs", CenterX - 150, OrbY - 30, 300, 22, 15, Gold, TextAlignmentOptions.Center);
        foreach (AdvancedMaterial m in AdvancedMaterials.All)
        {
            int have = AdvancedMaterials.Count(m.Key);
            Image box = R.MakeImage(Content, "Orb_" + m.Key, new Color(0.035f, 0.03f, 0.03f, 0.97f));
            RectTransform br = box.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0, 1);
            br.pivot = new Vector2(0, 1);
            br.anchoredPosition = new Vector2(x, -OrbY);
            br.sizeDelta = new Vector2(OrbSize, OrbSize);
            dynamic.Add(box.gameObject);
            Image art = R.MakeImage(box.transform, "Art", have > 0 ? Color.white : new Color(1, 1, 1, 0.35f));
            art.sprite = OrbArt.Get(m.Key, m.Tint, m.Glow);
            art.preserveAspect = true;
            art.raycastTarget = false;
            Stretch(art.rectTransform, 5);
            SlotFrame(box, m.Glow * 0.8f);
            GameObject count = Label(have.ToString("N0"), OrbSize - 54, OrbSize - 22, 50, 20, 15, have > 0 ? Color.white : Dim, TextAlignmentOptions.BottomRight, box.transform);
            dynamic.Remove(count); // it goes with the box
            string key = m.Key;
            Button b = box.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener((UnityEngine.Events.UnityAction)(() => UseOrb(key)));
            orbRects.Add((br, m));
            x += OrbSize + 16;
        }
    }

    private static void UseOrb(string key)
    {
        if (animating)
        {
            return;
        }
        string why;
        if (AdvancedMaterials.Count(key) < 1)
        {
            why = "You have no " + AdvancedMaterials.All.Find(m => m.Key == key)?.Name + "s.";
        }
        else if (key == AdvancedMaterials.EncapsulationOrb)
        {
            why = Items_Encapsulation.WhyNot(item);
        }
        else
        {
            why = "";
        }
        if (why.Length > 0)
        {
            status = why;
            statusColour = Bad;
            dirty = true;
            return;
        }
        StartHammer(key);
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

        OrbRow();
        SlotBox(item, CenterX - IconW / 2, SlotY);

        if (status.Length > 0)
        {
            Label(status, CenterX - 260, SlotY + IconH + 12, 520, 40, 14, statusColour, TextAlignmentOptions.Top, caps: false);
        }
        if (item != null)
        {
            AffixPlates(SlotY + IconH + 78);
        }
    }

    // ---------------------------------------------------------------- the item's affixes

    private sealed class Line
    {
        public string Text;
        public int Tier;
        public bool Fractured;
        public int Group; // 0 prefix, 1 suffix, 2 sealed, 3 primordial, 4 corrupted
        public int Rows;
    }

    private static readonly string[] GroupName = { "Prefixes", "Suffixes", "Sealed", "Primordial", "Corrupted" };

    private static readonly Color[] GroupColour =
    {
        new(0.93f, 0.76f, 0.38f), // prefix: gold
        new(0.42f, 0.82f, 0.74f), // suffix: teal
        new(0.68f, 0.52f, 0.92f), // sealed: violet
        new(0.95f, 0.5f, 0.3f), // primordial: ember
        new(0.82f, 0.28f, 0.5f), // corrupted: crimson
    };

    private static readonly Color FracturedColour = new(0.45f, 0.8f, 1f);

    private const float PlateW = 306, TextW = PlateW - 62 - 16, PlateFont = 12;

    /// <summary>How many rows of text a wording takes in the width of a plate.</summary>
    private static int RowsOf(string text)
    {
        int perRow = Math.Max(10, (int)(TextW / (PlateFont * 0.5f)));
        int rows = 0;
        foreach (string part in text.Split('\n'))
        {
            rows += Math.Max(1, (part.Length + perRow - 1) / perRow);
        }
        return Math.Min(rows, 5);
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
            int group = a.IsSealedCorrupted ? 4 : a.IsSealedPrimordial ? 3 : a.IsSealed ? 2 : a.affixType == AffixList.AffixType.PREFIX ? 0 : 1;
            list.Add(new Line
            {
                Text = text,
                Tier = a.DisplayTier,
                Group = group,
                Fractured = Items_Fracture.IsFractured(item, a),
                Rows = RowsOf(text),
            });
        }
        return list;
    }

    /// <summary>
    /// The affixes as plates under the item: prefixes in a column on the left, suffixes on the right, and below them the
    /// sealed, primordial and corrupted ones. Each kind has its own colour; a fractured affix is blue with a diamond.
    /// </summary>
    private static void AffixPlates(float top)
    {
        const float gap = 12;
        List<Line> all = ReadAffixes();
        float leftX = CenterX - PlateW - gap / 2, rightX = CenterX + gap / 2;
        float y = top;

        float prefixEnd = Column(all.Where(l => l.Group == 0).ToList(), leftX, y, 0);
        float suffixEnd = Column(all.Where(l => l.Group == 1).ToList(), rightX, y, 1);
        y = Math.Max(prefixEnd, suffixEnd);

        List<Line> special = all.Where(l => l.Group >= 2).ToList();
        if (special.Count > 0)
        {
            y += 8;
            Label("Sealed, primordial and corrupted", CenterX - 200, y, 400, 20, 13, new Color(0.72f, 0.66f, 0.8f), TextAlignmentOptions.Center);
            y += 24;
            for (int i = 0; i < special.Count; i += 2)
            {
                float h = 0;
                for (int k = i; k < Math.Min(i + 2, special.Count); k++)
                {
                    h = Math.Max(h, PlateHeight(special[k]));
                }
                for (int k = i; k < Math.Min(i + 2, special.Count); k++)
                {
                    Plate(special[k], k - i == 0 ? leftX : rightX, y, h);
                }
                y += h + 8;
            }
        }
    }

    private static float PlateHeight(Line l) => 20 + l.Rows * 15;

    /// <summary>One column of plates under a coloured heading. Returns where it ends.</summary>
    private static float Column(List<Line> lines, float x, float y, int group)
    {
        Color colour = GroupColour[group];
        Label(GroupName[group], x, y - 24, PlateW, 20, 14, colour, TextAlignmentOptions.Center);
        R.Part(Content, "Rule", colour * 0.6f, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + 40, -(y - 4)), new Vector2(PlateW - 80, 1));
        foreach (Line l in lines)
        {
            float h = PlateHeight(l);
            Plate(l, x, y, h);
            y += h + 8;
        }
        return y;
    }

    private static void Plate(Line l, float x, float y, float h)
    {
        const float pad = 10;
        Color accent = l.Fractured ? FracturedColour : GroupColour[l.Group];
        Image plate = R.MakeImage(Content, "AffixPlate", l.Fractured ? new Color(0.035f, 0.07f, 0.095f, 0.93f) : new Color(0.04f, 0.034f, 0.03f, 0.92f));
        plate.raycastTarget = false;
        RectTransform pr = plate.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0, 1);
        pr.pivot = new Vector2(0, 1);
        pr.anchoredPosition = new Vector2(x, -y);
        pr.sizeDelta = new Vector2(PlateW, h);
        dynamic.Add(plate.gameObject);
        Transform t = plate.transform;
        R.Part(t, "Bar", accent, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(4, 0));
        R.Part(t, "Top", accent * 0.45f, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 1));
        R.Part(t, "Bottom", accent * 0.45f, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 1));
        Label("T" + l.Tier, pad + 4, 8, 40, 16, 12, accent, TextAlignmentOptions.Left, t);
        GameObject text = Label(l.Text, pad + 48, 8, TextW + 14, h - 12, PlateFont,
            l.Fractured ? new Color(0.62f, 0.9f, 1f) : new Color(0.88f, 0.84f, 0.76f), TextAlignmentOptions.TopLeft, t, caps: false);
        TextMeshProUGUI tmp = text.GetComponent<TextMeshProUGUI>();
        tmp.enableAutoSizing = true; // a long wording shrinks to fit the plate instead of running over it
        tmp.fontSizeMin = 8;
        tmp.fontSizeMax = PlateFont;
        if (l.Fractured)
        {
            Image diamond = R.MakeImage(t, "Diamond", Color.white);
            diamond.sprite = OrbArt.Diamond();
            diamond.preserveAspect = true;
            diamond.raycastTarget = false;
            RectTransform dr = diamond.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(1, 1);
            dr.pivot = new Vector2(1, 1);
            dr.anchoredPosition = new Vector2(-6, -6);
            dr.sizeDelta = new Vector2(14, 14);
        }
    }

    // ---------------------------------------------------------------- hover: the orbs' own tooltip and the item's

    private static int hoveredNow = -1; // an orb's index, -2 for the item, -1 for nothing
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
            manager.OpenTooltip(unpacked, TooltipItemManager.SlotType.CRAFTING, Vector2.zero, at.position, at.gameObject, at.rect.size);
            tooltipOpen = true;
        }
    }

    private static void TickHover()
    {
        Vector2 mouse = Input.mousePosition;
        UnityEngine.Camera cam = PointerCamera();
        int target = -1;
        for (int i = 0; i < orbRects.Count; i++)
        {
            if (orbRects[i].Rect != null && RectTransformUtility.RectangleContainsScreenPoint(orbRects[i].Rect, mouse, cam))
            {
                target = i;
                break;
            }
        }
        if (target < 0 && !animating && item != null && slotRect != null && RectTransformUtility.RectangleContainsScreenPoint(slotRect, mouse, cam))
        {
            target = -2;
        }
        if (target == hoveredNow)
        {
            return;
        }
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
                OpenGameTooltip(Drop.OrbDrop.TooltipCarrier(), orbRects[target].Rect);
            }
            else if (target == -2)
            {
                OpenGameTooltip(item, slotRect);
            }
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Advanced Forge tooltip failed: " + ex.Message);
        }
    }

    // ---------------------------------------------------------------- the hammer

    private static readonly List<GameObject> effects = new();
    private static bool animating;
    private static float animStart;
    private static string pendingKey;
    private static bool applied;
    private static GameObject hammer;
    private static Image hammerHead, flash;
    private static CanvasGroup hammerGroup;
    private static Vector2 shakeBase;

    private sealed class Spark
    {
        public GameObject Go;
        public RectTransform Rect;
        public Image Img;
        public Vector2 Pos, Vel;
        public float Born, Life;
    }

    private static readonly List<Spark> sparks = new();

    private static Vector2 SlotCentre => new(CenterX, -(SlotY + IconH / 2));
    private const float StrikeAngle = 38f, RaisedAngle = -34f, WindupAngle = -58f;

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

    private static Image Piece(Transform parent, string name, Color colour, float w, float h, Vector2 pos, Vector2 pivot)
    {
        Image img = R.MakeImage(parent, name, colour);
        img.raycastTarget = false;
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0);
        r.pivot = pivot;
        r.sizeDelta = new Vector2(w, h);
        r.anchoredPosition = pos;
        return img;
    }

    private static void StartHammer(string key)
    {
        animating = true;
        applied = false;
        pendingKey = key;
        animStart = Time.unscaledTime;
        status = "";
        CloseTooltips();

        // the hammer: a worn wooden haft bound in iron, with a heavy dark head. Its grip is where it turns.
        hammer = Effect("Hammer");
        hammerGroup = hammer.AddComponent<CanvasGroup>();
        hammerGroup.blocksRaycasts = false;
        RectTransform hr = hammer.GetComponent<RectTransform>();
        hr.pivot = new Vector2(0.5f, 0);
        hr.sizeDelta = new Vector2(0, 0);
        Vector2 grip = SlotCentre + new Vector2(112, -138);
        hr.anchoredPosition = grip;
        hr.localRotation = Quaternion.Euler(0, 0, RaisedAngle);
        Transform t = hammer.transform;
        Piece(t, "HaftShadow", new Color(0, 0, 0, 0.5f), 20, 156, new Vector2(3, -3), new Vector2(0.5f, 0));
        Piece(t, "Haft", new Color(0.24f, 0.15f, 0.08f), 14, 152, Vector2.zero, new Vector2(0.5f, 0));
        Piece(t, "HaftLight", new Color(0.4f, 0.27f, 0.14f), 4, 146, new Vector2(-3, 2), new Vector2(0.5f, 0));
        for (int i = 0; i < 4; i++)
        {
            Piece(t, "Wrap" + i, new Color(0.08f, 0.07f, 0.06f), 17, 5, new Vector2(0, 10 + i * 9), new Vector2(0.5f, 0));
        }
        Piece(t, "Pommel", new Color(0.13f, 0.12f, 0.11f), 22, 10, new Vector2(0, -2), new Vector2(0.5f, 0));
        for (int i = 0; i < 2; i++)
        {
            Piece(t, "Band" + i, new Color(0.17f, 0.16f, 0.15f), 19, 7, new Vector2(0, 118 + i * 12), new Vector2(0.5f, 0));
        }
        // head
        Piece(t, "HeadShadow", new Color(0, 0, 0, 0.45f), 98, 52, new Vector2(4, 150), new Vector2(0.5f, 0));
        hammerHead = Piece(t, "Head", new Color(0.2f, 0.2f, 0.22f), 94, 48, new Vector2(0, 152), new Vector2(0.5f, 0));
        Piece(t, "HeadTop", new Color(0.36f, 0.37f, 0.4f), 90, 7, new Vector2(0, 193), new Vector2(0.5f, 0));
        Piece(t, "HeadEdgeL", new Color(0.1f, 0.1f, 0.11f), 14, 48, new Vector2(-40, 152), new Vector2(0.5f, 0));
        Piece(t, "HeadEdgeR", new Color(0.1f, 0.1f, 0.11f), 14, 48, new Vector2(40, 152), new Vector2(0.5f, 0));
        Piece(t, "HeadBottom", new Color(0.07f, 0.07f, 0.08f), 94, 6, new Vector2(0, 152), new Vector2(0.5f, 0));
        AdvancedMaterial m = AdvancedMaterials.All.Find(x => x.Key == key);
        Color glow = m != null ? m.Glow : Color.white;
        Image rune = Piece(t, "Rune", glow, 12, 12, new Vector2(0, 166), new Vector2(0.5f, 0.5f));
        rune.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        rune.color = new Color(glow.r, glow.g, glow.b, 0.9f);

        GameObject f = Effect("Flash");
        RectTransform fr = f.GetComponent<RectTransform>();
        fr.anchoredPosition = new Vector2(CenterX - IconW / 2, -SlotY);
        fr.sizeDelta = new Vector2(IconW, IconH);
        flash = R.MakeImage(f.transform, "F", new Color(glow.r, glow.g, glow.b, 0f));
        flash.raycastTarget = false;
        Stretch(flash.rectTransform, 0);
        shakeBase = new Vector2(CenterX - IconW / 2, -SlotY);
    }

    private static float Ease(float u) => u * u * (3f - 2f * u);

    private static void TickAnimation()
    {
        float now = Time.unscaledTime;
        TickSparks(now);
        if (!animating)
        {
            return;
        }
        float t = now - animStart;
        float angle;
        const float windupEnd = 0.30f, impact = 0.50f, rebound = 0.64f, lift = 1.05f;
        if (t < windupEnd)
        {
            angle = Mathf.Lerp(RaisedAngle, WindupAngle, Ease(t / windupEnd));
        }
        else if (t < impact)
        {
            float u = (t - windupEnd) / (impact - windupEnd);
            angle = Mathf.Lerp(WindupAngle, StrikeAngle, u * u); // it falls, faster and faster
        }
        else if (t < rebound)
        {
            angle = Mathf.Lerp(StrikeAngle, StrikeAngle - 10f, Ease((t - impact) / (rebound - impact)));
        }
        else
        {
            angle = Mathf.Lerp(StrikeAngle - 10f, RaisedAngle, Ease(Mathf.Clamp01((t - rebound) / (lift - rebound))));
        }
        if (hammer != null)
        {
            hammer.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, angle);
            hammerGroup.alpha = t < lift - 0.2f ? 1f : Mathf.Clamp01((lift - t) / 0.2f);
        }
        if (!applied && t >= impact)
        {
            applied = true;
            Impact(now);
        }
        // the item jolts, then settles
        if (slotRect != null)
        {
            float since = t - impact;
            Vector2 shake = Vector2.zero;
            if (since >= 0 && since < 0.45f)
            {
                float amp = 11f * (1f - since / 0.45f);
                shake = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * amp;
            }
            slotRect.anchoredPosition = shakeBase + shake;
        }
        if (flash != null && applied)
        {
            Color c = flash.color;
            c.a = Mathf.Clamp01(0.75f * (1f - (t - impact) / 0.4f));
            flash.color = c;
        }
        if (t >= lift)
        {
            EndAnimationNow();
            dirty = true;
        }
    }

    private static void Impact(float now)
    {
        AdvancedMaterial m = AdvancedMaterials.All.Find(x => x.Key == pendingKey);
        bool ok = false;
        string message = "";
        if (pendingKey == AdvancedMaterials.EncapsulationOrb)
        {
            ok = Items_Encapsulation.UseOn(item, out message);
        }
        status = message;
        statusColour = ok ? Good : Bad;
        Color a = m != null ? m.Glow : Color.white;
        Color b = ok ? Color.white : new Color(1f, 0.55f, 0.4f);
        Vector2 at = SlotCentre;
        for (int i = 0; i < 46; i++)
        {
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            float speed = UnityEngine.Random.Range(140f, 520f);
            AddSpark(at, Color.Lerp(a, b, UnityEngine.Random.value), UnityEngine.Random.Range(3f, 7f),
                new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.8f + 0.25f) * speed, UnityEngine.Random.Range(0.35f, 0.9f));
        }
        dirty = false; // the window is rebuilt when the hammer is done, so the item is not redrawn mid-swing
    }

    private static void AddSpark(Vector2 at, Color colour, float size, Vector2 vel, float life)
    {
        GameObject go = Effect("Spark");
        RectTransform r = go.GetComponent<RectTransform>();
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(size, size * UnityEngine.Random.Range(1f, 2.4f));
        r.anchoredPosition = at;
        r.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 90f));
        Image img = R.MakeImage(go.transform, "S", colour);
        img.raycastTarget = false;
        Stretch(img.rectTransform, 0);
        sparks.Add(new Spark { Go = go, Rect = r, Img = img, Pos = at, Vel = vel, Born = Time.unscaledTime, Life = life });
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
                    Object.Destroy(s.Go);
                }
                sparks.RemoveAt(i);
                continue;
            }
            s.Vel.y -= 520f * dt;
            s.Pos += s.Vel * dt;
            s.Rect.anchoredPosition = s.Pos;
            Color c = s.Img.color;
            c.a = 1f - age / s.Life;
            s.Img.color = c;
        }
    }

    private static void EndAnimationNow()
    {
        animating = false;
        foreach (GameObject go in effects)
        {
            if (go != null)
            {
                Object.Destroy(go);
            }
        }
        effects.Clear();
        sparks.Clear();
        hammer = null;
        hammerHead = null;
        flash = null;
        hammerGroup = null;
        if (slotRect != null)
        {
            slotRect.anchoredPosition = shakeBase;
        }
    }
}
