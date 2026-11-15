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

    private static GameObject Label(string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align, Transform parent = null, List<GameObject> keep = null)
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
        t.fontStyle = FontStyles.Normal; // the forge title style is lower case
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
        Label("Orbs", CenterX - 150, OrbY - 34, 300, 26, 20, Gold, TextAlignmentOptions.Center);
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
            Label(status, CenterX - 260, SlotY + IconH + 14, 520, 44, 17, statusColour, TextAlignmentOptions.Top);
        }
        if (item != null)
        {
            AffixPlates(SlotY + IconH + 70);
        }
    }

    // ---------------------------------------------------------------- the item's affixes

    private sealed class Line
    {
        public string Text;
        public int Tier;
        public string Kind;
        public bool Fractured, Sealed;
        public int Lines;
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
                    text = string.Join("\n", text.Split('\n').Select(l => l.Trim().TrimStart('\u25C6', ' ')).Where(l => l.Length > 0));
                }
            }
            catch (Exception)
            {
            }
            string kind = a.IsSealedPrimordial ? "Primordial" : a.IsSealed ? "Sealed" : a.affixType == AffixList.AffixType.PREFIX ? "Prefix" : "Suffix";
            list.Add(new Line
            {
                Text = text,
                Tier = a.DisplayTier,
                Kind = kind,
                Fractured = Items_Fracture.IsFractured(item, a),
                Sealed = a.IsSealed,
                Lines = Math.Min(3, text.Split('\n').Length),
            });
        }
        // prefixes first, then suffixes, then the seals
        return list.OrderBy(l => l.Sealed ? 2 : l.Kind == "Prefix" ? 0 : 1).ToList();
    }

    /// <summary>The affixes as plates in two columns under the item: tier, wording, and what kind of affix it is.</summary>
    private static void AffixPlates(float top)
    {
        const float plateW = 306, gap = 12, pad = 10;
        List<Line> lines = ReadAffixes();
        float x0 = CenterX - plateW - gap / 2;
        float y = top;
        Label("Affixes", CenterX - 100, y - 30, 200, 24, 17, Gold, TextAlignmentOptions.Center);
        for (int i = 0; i < lines.Count; i += 2)
        {
            float h = 0;
            for (int k = i; k < Math.Min(i + 2, lines.Count); k++)
            {
                h = Math.Max(h, 34 + lines[k].Lines * 16);
            }
            for (int k = i; k < Math.Min(i + 2, lines.Count); k++)
            {
                Line l = lines[k];
                float x = x0 + (k - i) * (plateW + gap);
                Color edge = l.Fractured ? new Color(0.45f, 0.8f, 1f) : l.Sealed ? new Color(0.6f, 0.45f, 0.78f) : new Color(0.42f, 0.36f, 0.24f);
                Image plate = R.MakeImage(Content, "AffixPlate", l.Fractured ? new Color(0.04f, 0.075f, 0.1f, 0.92f) : new Color(0.04f, 0.034f, 0.03f, 0.9f));
                plate.raycastTarget = false;
                RectTransform pr = plate.rectTransform;
                pr.anchorMin = pr.anchorMax = new Vector2(0, 1);
                pr.pivot = new Vector2(0, 1);
                pr.anchoredPosition = new Vector2(x, -y);
                pr.sizeDelta = new Vector2(plateW, h);
                dynamic.Add(plate.gameObject);
                Transform t = plate.transform;
                R.Part(t, "Bar", edge, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(4, 0));
                R.Part(t, "Top", edge * 0.5f, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 1));
                R.Part(t, "Bottom", edge * 0.5f, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 1));
                Label("T" + l.Tier, pad + 4, pad - 2, 34, 18, 12, new Color(0.62f, 0.6f, 0.56f), TextAlignmentOptions.Left, t);
                Label(l.Kind, pad + 4, pad + 14, 56, 16, 10, new Color(0.5f, 0.48f, 0.44f), TextAlignmentOptions.Left, t);
                Label(l.Text, pad + 62, pad - 2, plateW - 62 - pad - 4, h - 12, 13,
                    l.Fractured ? new Color(0.6f, 0.9f, 1f) : l.Sealed ? new Color(0.8f, 0.72f, 0.92f) : new Color(0.88f, 0.84f, 0.76f),
                    TextAlignmentOptions.TopLeft, t);
                if (l.Fractured)
                {
                    Label("◆ Fractured", plateW - 90, h - 20, 82, 16, 10, new Color(0.45f, 0.8f, 1f), TextAlignmentOptions.Right, t);
                }
            }
            y += h + 8;
        }
    }

    // ---------------------------------------------------------------- hover: the orbs' own tooltip and the item's

    private static readonly List<GameObject> tip = new();
    private static int hoveredOrb = -1;
    private static bool hoveredItem;
    private static RectTransform tipRect;

    private static UnityEngine.Camera PointerCamera()
    {
        Canvas canvas = root.GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private static void CloseTooltips()
    {
        foreach (GameObject go in tip)
        {
            if (go != null)
            {
                Object.Destroy(go);
            }
        }
        tip.Clear();
        tipRect = null;
        hoveredOrb = -1;
        if (hoveredItem)
        {
            hoveredItem = false;
            try
            {
                TooltipItemManager.ForceClose();
            }
            catch (Exception)
            {
            }
        }
    }

    private static void TickHover()
    {
        Vector2 mouse = Input.mousePosition;
        UnityEngine.Camera cam = PointerCamera();
        int orb = -1;
        for (int i = 0; i < orbRects.Count; i++)
        {
            if (orbRects[i].Rect != null && RectTransformUtility.RectangleContainsScreenPoint(orbRects[i].Rect, mouse, cam))
            {
                orb = i;
                break;
            }
        }
        bool overItem = !animating && item != null && slotRect != null && RectTransformUtility.RectangleContainsScreenPoint(slotRect, mouse, cam);
        if (orb != hoveredOrb)
        {
            foreach (GameObject go in tip)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            tip.Clear();
            tipRect = null;
            hoveredOrb = orb;
            if (orb >= 0)
            {
                BuildOrbTooltip(orbRects[orb].Material);
            }
        }
        if (tipRect != null)
        {
            PlaceTooltip(mouse, cam);
        }
        if (overItem != hoveredItem)
        {
            hoveredItem = overItem;
            try
            {
                if (overItem)
                {
                    TooltipItemManager manager = Object.FindObjectOfType<TooltipItemManager>();
                    ItemDataUnpacked data = item.TryCast<ItemDataUnpacked>();
                    if (manager != null && data != null)
                    {
                        manager.OpenTooltip(data, TooltipItemManager.SlotType.CRAFTING, Vector2.zero, slotRect.position, slotRect.gameObject, slotRect.rect.size);
                    }
                }
                else
                {
                    TooltipItemManager.ForceClose();
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Advanced Forge tooltip failed: " + ex.Message);
            }
        }
    }

    private static void BuildOrbTooltip(AdvancedMaterial m)
    {
        tipRect = OrbTooltipPlate.Build(Content, bodyFont ?? headFont, m);
        tip.Add(tipRect.gameObject);
    }

    private static void PlaceTooltip(Vector2 mouse, UnityEngine.Camera cam)
    {
        RectTransform content = Content.GetComponent<RectTransform>();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(content, mouse, cam, out Vector2 local))
        {
            return;
        }
        Rect rect = content.rect;
        float x = local.x - rect.xMin + 22;
        float y = local.y - rect.yMax - 18;
        // keep it inside the window
        x = Mathf.Min(x, rect.width - tipRect.sizeDelta.x - 8);
        y = Mathf.Max(y, -(rect.height - 8));
        tipRect.anchoredPosition = new Vector2(Mathf.Max(8, x), y);
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
