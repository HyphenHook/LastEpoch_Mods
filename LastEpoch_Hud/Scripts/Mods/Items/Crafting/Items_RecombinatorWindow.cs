using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.AssetBundles;
using Il2CppLE.AssetManagement;
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
internal static class Items_RecombinatorWindow
{
    private const string ForgePath = "GUI/Panel System/Panel Stacks/Left Panel Stack/ForgePanel (Clone)";
    private const string ButtonName = "RecombinateButton";
    private const string RootName = "RecombinatorWindow";

    private static GameObject root;
    private static GameObject buttonTemplate; // the forge's own button, used as the model for ours
    private static GameObject labelTemplate; // the forge title, used as the model for text
    private static TMP_FontAsset headFont, bodyFont; // the game's own fonts, read from the forge

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

    private const float IconW = 140, IconH = 196, InY = 128, ResY = 476;
    private const float Left = 48, ColW = 320, Gap = 30;

    private sealed class Icon
    {
        public Sprite Back, Content;
        public Color BackColor = Color.white, ContentColor = Color.white;
        public LoadRef<Sprite> Load; // our own load of the item's sprite: it stays loaded until we let it go
        public float Born;
        public GameObject Holder; // a hidden copy of an inventory item widget, used only for the game's own background
        public InventoryItemUI Copy;

        /// <summary>True once the sprite is loaded and has been picked up.</summary>
        public bool Poll()
        {
            ReadBackground();
            if (Content != null)
            {
                return true;
            }
            if (Load != null && Load.Status == AssetStatus.Loaded && !Load.AssetOrNull.IsNullOrDestroyed())
            {
                Content = Load.AssetOrNull;
                return true;
            }
            return false;
        }

        /// <summary>Takes the background sprite and colour the game picked for this item.</summary>
        public void ReadBackground()
        {
            if (Back != null || Copy.IsNullOrDestroyed() || Copy.backgroundImage.IsNullOrDestroyed())
            {
                return;
            }
            if (Copy.backgroundImage.sprite != null)
            {
                Back = Copy.backgroundImage.sprite;
                BackColor = Copy.backgroundImage.color;
                if (BackColor.a < 0.2f)
                {
                    BackColor.a = 1f;
                }
            }
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
                Holder = null;
            }
            Copy = null;
            Load = null;
            Content = null;
            Back = null;
        }
    }

    private sealed class Ghost
    {
        public GameObject Go;
        public RectTransform Rect;
        public CanvasGroup Group;
        public Vector2 From, To;
        public int Side;
        public float Start, Duration;
    }

    private static readonly Icon[] icons = new Icon[3]; // 0 and 1: the input slots, 2: the result
    private static readonly long[] iconFor = new long[3];
    private static readonly RectTransform[] slotRects = new RectTransform[3];
    private static readonly ItemData[] slotItems = new ItemData[3];
    private static readonly List<Ghost> ghosts = new();
    private static int hovered = -1;

    // The pour: embers run from both items along the trenches into the result.
    private static bool pouring;
    private static float pourStart;
    private static float pourTotal = 2.7f; // longer when bones are crushed too
    private static bool pourBones;
    private static float crushAt = -10f;
    private static readonly RectTransform[] slotBoxes = new RectTransform[3];
    private static readonly Vector2[] slotBoxBase = new Vector2[3];

    private sealed class Piece
    {
        public GameObject Go;
        public RectTransform Rect;
        public CanvasGroup Group;
        public bool IsDrop; // a falling bone (or inward spark); otherwise a flying shard
        public bool Inward;
        public Vector2 From, To, Velocity;
        public float Start, Life, Spin, Gravity;
    }

    private static readonly List<Piece> pieces = new();
    private static readonly ItemData[] pourItems = new ItemData[2];
    private static readonly CanvasGroup[] slotGroups = new CanvasGroup[3];
    private static readonly List<Image> trenchInner = new();
    private static readonly List<Vector2>[] pourPath = new List<Vector2>[2];
    private static readonly float[] pourLength = new float[2];
    private static bool pendingFlash;
    private static bool lastFailed;
    private static int escapeFrames;
    private static float flashAt = -10f;
    private static bool chargeDone;
    private static RectTransform contentRect;
    private static Vector2 contentBase;
    private sealed class Flash
    {
        public Image Glow, Overlay, Wash;
        public List<Image> Rays = new();
        public float Start;
    }
    private static readonly List<Flash> flashes = new();
    private static readonly Color TrenchOuter = new(0.48f, 0.38f, 0.20f, 0.95f);
    private static readonly Color TrenchDark = new(0.025f, 0.02f, 0.015f, 1f);
    private static readonly Color TrenchGlow = new(1f, 0.45f, 0.10f, 1f);

    private static readonly List<GameObject> dynamic = new(); // rebuilt on each refresh

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
                        SpawnFlash();
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
        StripGameLogic(button);
        SetText(button, "Recombinate");
        RectTransform rect = button.GetComponent<RectTransform>();
        RectTransform source = template.GetComponent<RectTransform>();
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.sizeDelta = new Vector2(150, source.sizeDelta.y);
        rect.anchoredPosition = new Vector2(source.anchoredPosition.x + source.sizeDelta.x + 12, source.anchoredPosition.y);
        OnClick(button, OpenFromForge);
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
        ResetTunnel();
        pendingFlash = false;
        Array.Clear(pourItems, 0, 2);
        ReturnItems();
        if (root != null)
        {
            root.SetActive(false);
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
        headFont = labelTemplate?.GetComponent<TextMeshProUGUI>()?.font;
        bodyFont = main.Find("SealedAffixInfoHolder/AffixName")?.GetComponent<TextMeshProUGUI>()?.font ?? headFont;
        Main.logger_instance?.Msg("Recombinator fonts: heading " + (headFont != null ? headFont.name : "none") + ", body " + (bodyFont != null ? bodyFont.name : "none"));
        if (buttonTemplate == null || labelTemplate == null)
        {
            return false;
        }

        root = new GameObject(RootName);
        root.AddComponent<RectTransform>();
        root.AddComponent<UIMouseListener>(); // tells the game the pointer is over UI, so clicks do not move the character
        root.transform.SetParent(parent, false);
        CopyRect(forge.GetComponent<RectTransform>(), root.GetComponent<RectTransform>());

        var content = new GameObject("Content");
        content.AddComponent<RectTransform>();
        content.transform.SetParent(root.transform, false);
        CopyRect(main.GetComponent<RectTransform>(), content.GetComponent<RectTransform>());
        contentRect = content.GetComponent<RectTransform>();
        contentBase = contentRect.anchoredPosition;

        // The forge's own backdrop, frame and header, so the window matches the game.
        foreach (string piece in new[] { "backgroundTile", "backgroundTile (1)", "Framing", "EterraFrameHeader", "Header" })
        {
            Transform source = main.Find(piece);
            if (source != null)
            {
                GameObject copy = Object.Instantiate(source.gameObject, content.transform, false);
                copy.name = piece;
                StripGameLogic(copy);
            }
        }
        Transform title = content.transform.Find("Header/Title");
        if (title != null)
        {
            SetTextOn(title.gameObject, "Recombinator");
        }
        Transform close = content.transform.Find("Header/Close_Button");
        if (close != null)
        {
            OnClick(close.gameObject, Close);
        }
        CaptureForgeArt(main);
        root.SetActive(false);
        return true;
    }

    internal static void CopyRect(RectTransform from, RectTransform to)
    {
        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.pivot = from.pivot;
        to.anchoredPosition = from.anchoredPosition;
        to.sizeDelta = from.sizeDelta;
    }

    /// <summary>Removes the parts of a copied forge object that would run forge logic or reset our text.</summary>
    internal static void StripGameLogic(GameObject go)
    {
        foreach (Component c in go.GetComponentsInChildren<Component>(true))
        {
            if (c == null)
            {
                continue;
            }
            string type = c.GetIl2CppType().Name;
            if (type == "LocalizeStringEvent" || type == "CraftingMaterialsToggleButton" || type == "TransformToggler"
                || type == "ContentSizeFitter" || type == "DOTweenAnimation" || type == "DOTweenVisualManager")
            {
                Object.Destroy(c);
            }
        }
        foreach (Button b in go.GetComponentsInChildren<Button>(true))
        {
            b.onClick = new Button.ButtonClickedEvent();
        }
    }

    internal static void OnClick(GameObject go, Action action)
    {
        Button b = go.GetComponent<Button>();
        if (b == null)
        {
            return;
        }
        b.onClick = new Button.ButtonClickedEvent();
        b.onClick.AddListener(action);
    }

    internal static void SetText(GameObject go, string text)
    {
        TextMeshProUGUI t = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (t != null)
        {
            t.text = text;
        }
    }

    private static void SetTextOn(GameObject go, string text)
    {
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (t != null)
        {
            t.text = text;
        }
    }

    // ---------------------------------------------------------------- the changing part of the window

    private static Transform Content => root.transform.Find("Content");

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

    private static GameObject Label(string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = Object.Instantiate(labelTemplate, Content, false);
        StripGameLogic(go);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        TMP_FontAsset font = size >= 18 ? headFont : bodyFont;
        if (font != null)
        {
            t.font = font;
        }
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = true;
        go.SetActive(true);
        dynamic.Add(go);
        return go;
    }

    private static GameObject Btn(string text, float x, float y, float w, float h, Action action, Color? tint = null)
    {
        GameObject go = Object.Instantiate(buttonTemplate, Content, false);
        StripGameLogic(go);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        SetText(go, text);
        TextMeshProUGUI t = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (t != null)
        {
            RectTransform tr = t.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(8, 2);
            tr.offsetMax = new Vector2(-8, -2);
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
        }
        HorizontalLayoutGroup layout = go.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            Object.Destroy(layout);
        }
        if (tint.HasValue)
        {
            Image img = go.GetComponent<Image>();
            if (img != null)
            {
                img.color = tint.Value;
            }
        }
        OnClick(go, action);
        go.SetActive(true);
        dynamic.Add(go);
        return go;
    }

    private static readonly Color Gold = new(0.93f, 0.78f, 0.38f);
    private static readonly Color Dim = new(0.65f, 0.62f, 0.58f);
    private static readonly Color Good = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Bad = new(0.9f, 0.4f, 0.35f);
    private static readonly Color Picked = new(1f, 0.85f, 0.45f);

    private static float SlotX(int i) => Left + 130 + i * 270; // two boxes spread with equal gaps
    private const float WinW = 670;
    private const float SlotBottom = InY + IconH;
    private const float RunY = SlotBottom + 46; // where the two pipes turn in toward the centre

    private static float CenterX => Left + WinW / 2;

    /// <summary>The trench from one item box: down, out to the side, down past the affixes, in to the centre.</summary>
    private static List<Vector2> TrenchPoints(int side, bool intoSlot)
    {
        float c = SlotX(side) + IconW / 2;
        return new List<Vector2>
        {
            new(c, SlotBottom), new(c, RunY), new(CenterX, RunY),
            new(CenterX, intoSlot ? ResY + IconH / 2 : ResY),
        };
    }

    private static GameObject tunnelRoot;
    private static readonly List<float> trenchAlong = new(); // how far along its side's path each inner block sits, 0..1
    private static readonly List<int> trenchSide = new();
    private static readonly List<Color> trenchBase = new();

    private static void TunnelBlock(Vector2 at, float w, float h, float rotation, Color color, bool track, int side, float along)
    {
        Image img = MakeImage(tunnelRoot.transform, "Rock", color);
        img.raycastTarget = false;
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(at.x, -at.y);
        r.sizeDelta = new Vector2(w, h);
        r.localRotation = Quaternion.Euler(0, 0, rotation);
        if (track)
        {
            trenchInner.Add(img);
            trenchBase.Add(color);
            trenchSide.Add(side);
            trenchAlong.Add(along);
        }
    }

    /// <summary>
    /// The soul pipes: old, grimy iron pipes with a lit channel inside. Nothing is a clean line: the body is built
    /// from uneven blocks, the edges are chipped, there is rust and soot, and the bolted flanges are worn. Built once.
    /// The channel is made of short pieces so the embers can light it as they pass.
    /// </summary>
    private static void Trenches()
    {
        if (tunnelRoot != null)
        {
            return;
        }
        tunnelRoot = new GameObject("Tunnel");
        RectTransform tr = tunnelRoot.AddComponent<RectTransform>();
        tunnelRoot.transform.SetParent(Content, false);
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        trenchInner.Clear();
        trenchAlong.Clear();
        trenchSide.Clear();
        trenchBase.Clear();

        var rng = new System.Random(9137);
        float R(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
        Color Iron() { float v = R(0.05f, 0.10f); return new Color(v, v * 0.92f, v * 0.84f); }
        Color rust = new(0.21f, 0.095f, 0.045f);
        Color soot = new(0.014f, 0.013f, 0.012f);
        Color dullBolt = new(0.36f, 0.29f, 0.20f);
        const float pipe = 17f, flangeEvery = 66f, step = 6f;

        for (int pass = 0; pass < 4; pass++)
        {
            for (int side = 0; side < 2; side++)
            {
                List<Vector2> pts = TrenchPoints(side, false);
                float total = PathLength(pts);
                float walked = 0;
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    Vector2 d = pts[i + 1] - pts[i];
                    float len = d.magnitude;
                    if (len < 0.01f)
                    {
                        continue;
                    }
                    bool horizontal = Mathf.Abs(d.x) > Mathf.Abs(d.y);
                    Vector2 dir = d / len;
                    Vector2 across = horizontal ? new Vector2(0, 1) : new Vector2(1, 0); // y points down, so this is the underside
                    for (float t = -pipe / 2; t <= len + pipe / 2; t += step)
                    {
                        Vector2 at = pts[i] + dir * t;
                        float along = Mathf.Clamp01((walked + t) / total);
                        if (pass == 0)
                        {
                            // body: uneven blocks of dark iron, slightly out of line
                            float alongSize = R(6.5f, 9f), acrossSize = pipe + R(-1.5f, 2f);
                            TunnelBlock(at + across * R(-0.9f, 0.9f), horizontal ? alongSize : acrossSize, horizontal ? acrossSize : alongSize, R(-2.5f, 2.5f), Iron(), false, side, along);
                            // the underside is sooty
                            TunnelBlock(at + across * (pipe / 2 - 2f), horizontal ? 7f : 3.5f, horizontal ? 3.5f : 7f, 0, new Color(0.03f, 0.028f, 0.026f), false, side, along);
                            // a few worn glints on the upper edge, never a clean line
                            if (rng.NextDouble() < 0.4)
                            {
                                float glint = R(0.15f, 0.24f);
                                TunnelBlock(at - across * (pipe / 2 - 1.5f), horizontal ? R(3f, 6f) : R(1.2f, 2f), horizontal ? R(1.2f, 2f) : R(3f, 6f), 0, new Color(glint, glint * 0.9f, glint * 0.78f), false, side, along);
                            }
                            // chips knocked out of the silhouette
                            if (rng.NextDouble() < 0.22)
                            {
                                float sgn = rng.NextDouble() < 0.5 ? -1f : 1f;
                                TunnelBlock(at + across * sgn * (pipe / 2 + 0.5f), R(2f, 4f), R(2f, 4f), R(0f, 45f), soot, false, side, along);
                            }
                        }
                        else if (pass == 1)
                        {
                            // grime: rust and soot patches
                            if (rng.NextDouble() < 0.2)
                            {
                                Color g = rng.NextDouble() < 0.5 ? rust : soot;
                                g.a = R(0.45f, 0.85f);
                                TunnelBlock(at + across * R(-pipe / 2, pipe / 2), R(3f, 9f), R(3f, 8f), R(0f, 90f), g, false, side, along);
                            }
                        }
                        else if (pass == 2)
                        {
                            // the channel: a dim ember glow that brightens as embers pass
                            if (t >= 0 && t <= len)
                            {
                                float v = R(0.8f, 1.15f);
                                Color baseColour = new(0.10f * v, 0.032f * v, 0.016f * v);
                                TunnelBlock(at + across * R(-0.8f, 0.8f), (horizontal ? 9f : 8f) + R(-1f, 1f), (horizontal ? 8f : 9f) + R(-1f, 1f), R(-3f, 3f), baseColour, true, side, along);
                            }
                        }
                    }
                    if (pass == 3)
                    {
                        // flanges: worn bands with dull bolts and a rust streak
                        for (float t = flangeEvery / 2 + R(-6f, 6f); t < len - 10; t += flangeEvery)
                        {
                            Vector2 at = pts[i] + dir * t;
                            float v = R(0.11f, 0.17f);
                            Color band = new(v, v * 0.84f, v * 0.7f);
                            TunnelBlock(at, (horizontal ? 9f : pipe + 8f) + R(-1f, 1f), (horizontal ? pipe + 8f : 9f) + R(-1f, 1f), R(-3f, 3f), band, false, side, 0);
                            TunnelBlock(at - across * (pipe / 2 + 2f), 3.5f, 3.5f, R(0f, 45f), dullBolt, false, side, 0);
                            TunnelBlock(at + across * (pipe / 2 + 2f), 3.5f, 3.5f, R(0f, 45f), dullBolt, false, side, 0);
                            TunnelBlock(at + across * R(-6f, 6f), R(2f, 4f), R(5f, 11f), R(-20f, 20f), rust, false, side, 0);
                        }
                    }
                    walked += len;
                }
                if (pass == 3)
                {
                    // worn elbow caps at the bends
                    foreach (Vector2 at in pts)
                    {
                        float v = R(0.10f, 0.15f);
                        TunnelBlock(at, 23f + R(-1f, 1f), 23f + R(-1f, 1f), R(-8f, 8f), new Color(v, v * 0.84f, v * 0.7f), false, side, 0);
                        TunnelBlock(at, 8f, 8f, R(30f, 60f), dullBolt, false, side, 0);
                        TunnelBlock(at, 4f, 4f, R(30f, 60f), soot, false, side, 0);
                        TunnelBlock(at + new Vector2(R(-7f, 7f), R(-7f, 7f)), R(3f, 7f), R(3f, 6f), R(0f, 90f), rust, false, side, 0);
                    }
                }
            }
        }
    }

    private static void Refresh()
    {
        dirty = false;
        ClearDynamic();
        CloseTooltip();
        Array.Clear(slotRects, 0, slotRects.Length);
        Array.Clear(slotItems, 0, slotItems.Length);
        Array.Clear(slotGroups, 0, slotGroups.Length);
        rows.Clear();
        cageRoot = null;

        Trenches();

        // the two item boxes, evenly spread, with each item's name above
        for (int i = 0; i < 2; i++)
        {
            int index = i;
            ItemData shown = pouring ? pourItems[i] : slots[i];
            SlotBox(shown, i, SlotX(i), InY, () => ClickSlot(index));
        }

        offer = null;
        string why = Items_Recombinator.WhyNot(slots[0], slots[1]);
        if (why.Length == 0)
        {
            offer = Items_Recombinator.Describe(slots[0], slots[1]);
        }

        // the affixes of both items; only pickable in deterministic mode
        if (offer != null && !pouring)
        {
            ListAffixes(offer.AffixesA, Left, ResY);
            ListAffixes(offer.AffixesB, Left + WinW - ColumnWidth, ResY);
        }

        // the result
        SlotBox(resultRevealed ? resultItem : null, 2, CenterX - IconW / 2, ResY, TakeResult);
        if (deterministic && primordial && (offer != null || pouring || resultItem != null))
        {
            BoneCage(CenterX - IconW / 2, ResY);
        }
        float y = ResY + IconH + 30;
        List<RecombAffix> picks = Picks();
        if (offer == null)
        {
            if (!pouring && resultItem == null && slots[0] != null && slots[1] != null)
            {
                Label(why, Left, y + 30, WinW, 40, 18, Bad, TextAlignmentOptions.Top);
            }
        }
        else
        {
            long gold;
            int embers;
            bool valid = true;
            if (deterministic)
            {
                gold = RecombinatorMath.Gold(offer.All, offer.AnyExalted);
                embers = RecombinatorMath.Embers(offer.All, offer.AnyExalted);
                double chance = RecombinatorMath.SuccessChance(picks, offer.CapacityA, offer.CapacityB, primordial);
                string fail = RecombinatorMath.Validate(picks, Math.Max(offer.CapacityA, offer.CapacityB));
                valid = picks.Count > 0 && fail.Length == 0;
                if (valid)
                {
                    Label((chance * 100).ToString("0.#") + "% success", Left, y, WinW, 28, 22, Gold, TextAlignmentOptions.Top);
                }
                else if (picks.Count > 0)
                {
                    Label(fail, Left, y, WinW, 28, 18, Bad, TextAlignmentOptions.Top);
                }
            }
            else
            {
                gold = RecombinatorMath.FlatGold;
                embers = RecombinatorMath.FlatEmbers;
            }
            bool usePrimordial = deterministic && primordial;
            int bones = usePrimordial ? RecombinatorMath.Bones(offer.All, offer.AnyExalted) : 0;
            long haveGold = HaveGold();
            int haveEmbers = HaveEmbers();
            long haveBones = HaveBones();
            bool canPay = haveGold >= gold && haveEmbers >= embers && haveBones >= bones;
            FindCurrencyIcons();
            var cost = new List<(Sprite, string, string, Color)>
            {
                (goldIcon, "gold", gold.ToString("N0"), haveGold >= gold ? Good : Bad),
                (emberIcon, "Soul Embers", embers.ToString("N0"), haveEmbers >= embers ? Good : Bad),
            };
            if (usePrimordial)
            {
                cost.Add((bonesIcon, "Ancient Bones", bones.ToString("N0"), haveBones >= bones ? Good : Bad));
            }
            ChipRow(CenterX, y + 28, 20, cost.ToArray());

            bool ready = canPay && valid;
            long cg = gold;
            int ce = embers;
            int cb = bones;
            float by = y + 70;
            Btn(deterministic ? "Deterministic" : "Indeterministic", Left + 43, by, 170, 44, () => SetMode(!deterministic), deterministic ? Picked : (Color?)null);
            Btn("Recombinate", Left + CenterX - Left - 110 - 0, by, 220, 44, () => DoRecombine(cg, ce, cb), ready ? (Color?)null : new Color(0.4f, 0.4f, 0.4f, 0.8f));
            float rx = Left + WinW / 2 + 110 + 18;
            Chip(goldIcon, "gold", haveGold.ToString("N0"), rx, by - 6, 15, Dim);
            Chip(emberIcon, "Soul Embers", haveEmbers.ToString("N0"), rx, by + 14, 15, Dim);
            if (deterministic)
            {
                Chip(bonesIcon, "Ancient Bones", haveBones.ToString("N0"), rx, by + 34, 15, Dim);
            }
            if (deterministic)
            {
                Btn("Primordial Recombination  (x1.5)", CenterX - 160, by + 56, 320, 38, () => { primordial = !primordial; message = ""; dirty = true; }, primordial ? Picked : (Color?)null);
            }
        }
        if (message.Length > 0)
        {
            Label(message, Left, y + 176, WinW, 40, 17, Gold, TextAlignmentOptions.Top);
        }
    }

    // ---------------------------------------------------------------- item slots, tooltips, animation

    private static Sprite goldIcon, bonesIcon, emberIcon;

    internal static Sprite GoldIcon => goldIcon;
    internal static Sprite BonesIcon => bonesIcon;
    internal static void EnsureCurrencyIcons() => FindCurrencyIcons();
    private static float nextIconSearch;
    private static int emberSearches;

    /// <summary>Finds the game's own currency icons (the inventory footer's gold and bones, the ember counter).</summary>
    private static void FindCurrencyIcons()
    {
        if ((goldIcon != null && bonesIcon != null && emberIcon != null) || Time.unscaledTime < nextIconSearch)
        {
            return;
        }
        nextIconSearch = Time.unscaledTime + 15f;
        emberSearches++;
        try
        {
            foreach (Image img in Object.FindObjectsOfType<Image>(true))
            {
                if (img == null || img.sprite == null)
                {
                    continue;
                }
                string n = img.gameObject.name;
                if (goldIcon == null && n == "Gold Coins Icon")
                {
                    goldIcon = img.sprite;
                }
                else if (bonesIcon == null && n == "Ancient Bones Icon")
                {
                    bonesIcon = img.sprite;
                }
            }
            if (emberIcon == null)
            {
                // the Soul Ember icon by its exact name (the game also has rune icons with "ember" in the name)
                Sprite buff = null;
                foreach (Sprite sp in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (sp == null)
                    {
                        continue;
                    }
                    if (sp.name.Equals("SoulEmber_Icon", StringComparison.OrdinalIgnoreCase))
                    {
                        emberIcon = sp;
                        break;
                    }
                    if (buff == null && sp.name.Equals("SoulEmber_Icon_Buff", StringComparison.OrdinalIgnoreCase))
                    {
                        buff = sp;
                    }
                }
                emberIcon ??= buff;
            }
            if (emberIcon == null)
            {
                // the Soul Ember status icon: the picture on the game's own ember counter
                var seen = new List<string>();
                foreach (SoulEmberCounterUI counter in Object.FindObjectsOfType<SoulEmberCounterUI>(true))
                {
                    if (counter == null)
                    {
                        continue;
                    }
                    foreach (Image img in counter.GetComponentsInChildren<Image>(true))
                    {
                        if (img == null || img.sprite == null)
                        {
                            continue;
                        }
                        seen.Add(img.gameObject.name + "/" + img.sprite.name);
                        string name = (img.gameObject.name + img.sprite.name).ToLowerInvariant();
                        if (emberIcon == null || name.Contains("icon") || name.Contains("ember"))
                        {
                            emberIcon = img.sprite;
                        }
                    }
                }
                Main.logger_instance?.Msg("Recombinator ember counter images: " + string.Join(", ", seen.Take(12)));
            }
            if (emberIcon == null)
            {
                // otherwise: any sprite named for soul embers, the icon one first
                Sprite best = null;
                var names = new List<string>();
                foreach (Sprite sp in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (sp == null)
                    {
                        continue;
                    }
                    string n = sp.name.ToLowerInvariant();
                    if (n.Contains("ember") && n.Contains("soul"))
                    {
                        names.Add(sp.name);
                        if (best == null || (n.Contains("icon") && !best.name.ToLowerInvariant().Contains("icon")))
                        {
                            best = sp;
                        }
                    }
                }
                emberIcon = best;
                Main.logger_instance?.Msg("Recombinator ember sprites: " + string.Join(", ", names.Take(12)));
            }
            Main.logger_instance?.Msg("Recombinator icons: gold " + (goldIcon != null) + ", bones " + (bonesIcon != null) + ", ember " + (emberIcon != null));
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Recombinator icon search failed: " + ex.Message);
        }
    }

    private const float ChipIcon = 24;

    private static float ChipWidth(string text, float size) => ChipIcon + 6 + text.Length * size * 0.58f;

    /// <summary>A currency amount with the game's icon in front; returns its width.</summary>
    private static float Chip(Sprite icon, string fallback, string text, float x, float y, float size, Color color)
    {
        float w = ChipWidth(text, size);
        float h = Mathf.Max(ChipIcon, size + 6);
        if (icon != null)
        {
            Image img = MakeImage(Content, "CurrencyIcon", Color.white);
            img.sprite = icon;
            img.preserveAspect = true;
            img.raycastTarget = false;
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -(y + (h - ChipIcon) / 2));
            r.sizeDelta = new Vector2(ChipIcon, ChipIcon);
            dynamic.Add(img.gameObject);
            Label(text, x + ChipIcon + 6, y, w, h, size, color, TextAlignmentOptions.MidlineLeft);
        }
        else if (fallback == "Soul Embers")
        {
            // no sprite found: a small glowing ember drawn by hand
            Image glow = MakeImage(Content, "EmberGlyph", new Color(1f, 0.5f, 0.12f, 0.9f));
            glow.raycastTarget = false;
            RectTransform gr = glow.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0, 1);
            gr.pivot = new Vector2(0.5f, 0.5f);
            gr.anchoredPosition = new Vector2(x + ChipIcon / 2, -(y + h / 2));
            gr.sizeDelta = new Vector2(15, 15);
            gr.localRotation = Quaternion.Euler(0, 0, 45);
            dynamic.Add(glow.gameObject);
            Image core = MakeImage(glow.transform, "Core", new Color(1f, 0.92f, 0.6f, 1f));
            core.raycastTarget = false;
            RectTransform cr = core.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(7, 7);
            Label(text, x + ChipIcon + 6, y, w, h, size, color, TextAlignmentOptions.MidlineLeft);
        }
        else
        {
            Label(text + " " + fallback, x, y, w + 60, h, size, color, TextAlignmentOptions.MidlineLeft);
            w += 60;
        }
        return w;
    }

    /// <summary>Several currency amounts side by side, centred on x.</summary>
    private static void ChipRow(float centre, float y, float size, params (Sprite Icon, string Fallback, string Text, Color Color)[] items)
    {
        const float gap = 30;
        float total = -gap;
        foreach (var it in items)
        {
            total += ChipWidth(it.Text, size) + gap + (it.Icon == null ? 60 : 0);
        }
        float x = centre - total / 2;
        foreach (var it in items)
        {
            x += Chip(it.Icon, it.Fallback, it.Text, x, y, size, it.Color) + gap;
        }
    }

    internal static void Notify(string text)
    {
        try
        {
            Notifications.ShowBazaarError(text); // the same red notice the Bazaar shows
        }
        catch (Exception)
        {
            message = text;
            dirty = true;
        }
    }

    private static RectTransform cageRoot;
    private static float cageReleaseAt = -10f;

    /// <summary>
    /// Primordial recombination: a dark, worn frame of old iron and bone round the result box. Nothing is clean: the
    /// bars are built of uneven pieces, the spikes bend and are chipped, and everything carries grime and cracks.
    /// All of it hangs from one object so it can squeeze shut.
    /// </summary>
    private static void BoneCage(float x, float y)
    {
        var rng = new System.Random(5521); // the same roughness every time, so it does not shimmer when redrawn
        float R(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
        Color Iron() { float v = R(0.035f, 0.07f); return new Color(v, v * 0.93f, v * 0.86f, 1f); }
        Color Bronze() { float v = R(0.20f, 0.30f); return new Color(v, v * 0.74f, v * 0.45f, 1f); }
        Color Bone() { float v = R(0.34f, 0.46f); return new Color(v, v * 0.93f, v * 0.78f, 1f); }
        Color boneDark = new(0.15f, 0.135f, 0.11f, 1f);
        Color crack = new(0.04f, 0.035f, 0.03f, 1f);
        Color ember = new(0.85f, 0.36f, 0.08f, 1f);
        Vector2 half = new(0.5f, 0.5f);
        float hw = IconW / 2f, hh = IconH / 2f;

        var rootGo = new GameObject("BoneCage");
        cageRoot = rootGo.AddComponent<RectTransform>();
        rootGo.transform.SetParent(Content, false);
        cageRoot.anchorMin = cageRoot.anchorMax = new Vector2(0, 1);
        cageRoot.pivot = half;
        cageRoot.anchoredPosition = new Vector2(x + hw, -(y + hh));
        cageRoot.sizeDelta = new Vector2(IconW, IconH);
        dynamic.Add(rootGo);

        void Seg(float cx, float cy, float w, float h, float rot, Color c)
        {
            Part(cageRoot, "Piece", c, half, half, half, new Vector2(cx, cy), new Vector2(w, h), rot);
        }

        // a bent, uneven, chipped spike from a point
        void Spike(Vector2 from, float angle, float length, float baseWidth)
        {
            const int parts = 7;
            float bend = R(-14f, 14f);
            Vector2 pos = from;
            float seg = length / parts;
            for (int i = 0; i < parts; i++)
            {
                if (i >= parts - 2 && rng.NextDouble() < 0.35)
                {
                    break; // a broken tip
                }
                float a = angle + bend * i / parts;
                float rad = a * Mathf.Deg2Rad;
                Vector2 dir = new(Mathf.Cos(rad), Mathf.Sin(rad));
                float k = 1f - (float)i / parts;
                float width = Mathf.Max(0.9f, baseWidth * Mathf.Pow(k, 1.2f) * R(0.72f, 1.15f));
                Vector2 c = pos + dir * (seg * 0.5f);
                Seg(c.x, c.y, seg + R(0.2f, 1.4f), width + 1.8f, a, boneDark);
                Seg(c.x, c.y, seg + R(0f, 1.2f), width, a + R(-3f, 3f), Bone());
                pos += dir * seg;
            }
        }

        // darkness behind, with a faint dull red heat
        Seg(0, 0, IconW + 84, IconH + 84, 0, new Color(0f, 0f, 0f, 0.45f));
        Seg(0, 0, IconW + 38, IconH + 38, R(-1f, 1f), new Color(0.45f, 0.08f, 0.03f, 0.11f));

        const float gap = 12;
        float l = -(hw + gap), r = hw + gap, t = hh + gap, bt = -(hh + gap);

        // bars: rows of uneven iron blocks, a broken bronze line, rust and soot
        void Bar(float x0, float y0, float x1, float y1)
        {
            bool horizontal = Mathf.Abs(y1 - y0) < 0.01f;
            float length = horizontal ? x1 - x0 : y1 - y0;
            for (float p = 0; p <= length; p += 7f)
            {
                float jx = R(-0.9f, 0.9f), jy = R(-0.9f, 0.9f);
                float cx = horizontal ? x0 + p : x0 + jx;
                float cy = horizontal ? y0 + jy : y0 + p;
                float thick = 6.5f + R(-1.2f, 1.6f);
                float along = 8f + R(0f, 2.5f);
                Seg(cx, cy, horizontal ? along : thick, horizontal ? thick : along, R(-2.5f, 2.5f), Iron());
                if (rng.NextDouble() < 0.6)
                {
                    float inner = (horizontal ? (y0 > 0 ? -1.8f : 1.8f) : (x0 > 0 ? -1.8f : 1.8f));
                    Seg(horizontal ? cx : cx + inner, horizontal ? cy + inner : cy, horizontal ? along : 1.3f, horizontal ? 1.3f : along, 0, Bronze());
                }
                if (rng.NextDouble() < 0.16)
                {
                    Seg(cx + R(-3f, 3f), cy + R(-3f, 3f), R(3f, 8f), R(3f, 7f), R(0f, 90f), new Color(0.2f, 0.09f, 0.04f, R(0.5f, 0.85f)));
                }
                if (rng.NextDouble() < 0.14)
                {
                    Seg(cx + R(-3f, 3f), cy + R(-3f, 3f), R(2f, 5f), R(2f, 5f), R(0f, 90f), crack);
                }
            }
            for (float p = 18; p < length - 10; p += R(19f, 26f))
            {
                Seg(horizontal ? x0 + p : x0, horizontal ? y0 : y0 + p, 3, 3, R(20f, 70f), Bronze());
            }
        }
        Bar(l, t, r, t);
        Bar(l, bt, r, bt);
        Bar(l, bt, l, t);
        Bar(r, bt, r, t);

        // corner plates, cracked and worn, each clawed by two bent bone spikes
        Vector2[] corner = { new(l, t), new(r, t), new(l, bt), new(r, bt) };
        float[] inward = { -45f, -135f, 45f, 135f };
        for (int k = 0; k < 4; k++)
        {
            Vector2 c = corner[k];
            Seg(c.x, c.y, 31f + R(-1f, 1f), 31f + R(-1f, 1f), 45f + R(-6f, 6f), Bronze());
            Seg(c.x, c.y, 26f + R(-1f, 1f), 26f + R(-1f, 1f), 45f + R(-6f, 6f), Iron());
            Seg(c.x, c.y, 13f, 13f, 45f + R(-8f, 8f), Bronze());
            Seg(c.x, c.y, 8f, 8f, 45f, ember);
            Seg(c.x + R(-3f, 3f), c.y + R(-3f, 3f), R(10f, 18f), 1.4f, R(0f, 180f), crack); // a crack across the plate
            Spike(c, inward[k] + 20f + R(-5f, 5f), 40f + R(-4f, 5f), 7f);
            Spike(c, inward[k] - 20f + R(-5f, 5f), 40f + R(-4f, 5f), 7f);
            Spike(c, inward[k] + 180f, 15f + R(-3f, 4f), 5f);
        }

        // studs on the lower and side bars
        foreach (Vector2 stud in new[] { new Vector2(0, bt), new Vector2(l, 0), new Vector2(r, 0) })
        {
            Seg(stud.x, stud.y, 17f, 17f, 45f + R(-6f, 6f), Bronze());
            Seg(stud.x, stud.y, 13f, 13f, 45f + R(-6f, 6f), Iron());
            Seg(stud.x, stud.y, 5f, 5f, 45f, ember);
        }

        // the crest above: a dim gem between two worn bone wings
        Seg(0, t, 24f, 24f, 45f + R(-5f, 5f), Bronze());
        Seg(0, t, 20f, 20f, 45f + R(-5f, 5f), Iron());
        Seg(0, t, 11f, 11f, 45f, ember);
        Seg(0, t, 4.5f, 4.5f, 45f, new Color(0.95f, 0.78f, 0.45f, 1f));
        Spike(new Vector2(13, t), 6f + R(-4f, 4f), 46f, 6f);
        Spike(new Vector2(-13, t), 174f + R(-4f, 4f), 46f, 6f);
        Spike(new Vector2(9, t + 6), 52f + R(-5f, 5f), 24f, 5f);
        Spike(new Vector2(-9, t + 6), 128f + R(-5f, 5f), 24f, 5f);
    }

    private static void TickCage(float now)
    {
        if (cageRoot == null)
        {
            return;
        }
        float scale;
        if (pouring && pourBones)
        {
            // it tightens while the embers gather, and snaps shut on every crushed bone
            scale = Mathf.Lerp(1f, 0.93f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((now - pourStart) / 1.8f)));
            float since = now - crushAt;
            if (since >= 0f && since < 0.3f)
            {
                scale -= 0.1f * (1f - since / 0.3f);
            }
        }
        else
        {
            scale = Mathf.Lerp(0.93f, 1f, Mathf.Clamp01((now - cageReleaseAt) / 0.5f));
        }
        cageRoot.localScale = new Vector3(scale, scale, 1f);
    }

    internal static Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.AddComponent<CanvasRenderer>();
        Image img = go.AddComponent<Image>();
        go.transform.SetParent(parent, false);
        img.color = color;
        return img;
    }

    /// <summary>A forge-style item slot: the item's own icon and background, the forge's border, a click action.</summary>
    private static void SlotBox(ItemData item, int id, float x, float y, Action click)
    {
        Image box = MakeImage(Content, "Slot", new Color(0.05f, 0.04f, 0.035f, 0.97f));
        RectTransform br = box.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0, 1);
        br.pivot = new Vector2(0, 1);
        br.anchoredPosition = new Vector2(x, -y);
        br.sizeDelta = new Vector2(IconW, IconH);
        dynamic.Add(box.gameObject);
        slotGroups[id] = box.gameObject.AddComponent<CanvasGroup>();
        slotBoxes[id] = br;
        slotBoxBase[id] = br.anchoredPosition;

        if (item != null)
        {
            Icon icon = IconFor(item, id);
            if (icon == null || icon.Back == null)
            {
                // only until the game's own background has been read
                box.color = Color.Lerp(new Color(0.05f, 0.04f, 0.035f, 0.97f), ItemColor(item), 0.2f);
            }
            if (icon != null)
            {
                if (icon.Back != null)
                {
                    Image back = MakeImage(box.transform, "ItemBack", icon.BackColor);
                    back.sprite = icon.Back;
                    back.raycastTarget = false;
                    Stretch(back.rectTransform, 4);
                }
                if (icon.Content != null)
                {
                    Image content = MakeImage(box.transform, "ItemIcon", icon.ContentColor);
                    content.sprite = icon.Content;
                    content.preserveAspect = true;
                    content.raycastTarget = false;
                    Stretch(content.rectTransform, 14);
                }
            }
            slotRects[id] = br;
            slotItems[id] = item;
        }
        SlotFrame(box, item != null ? ItemColor(item) : (Color?)null);
        Button button = box.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(click);
    }

    /// <summary>
    /// A solid frame for an item slot, drawn over the item art so it truly borders it: a rim in the item's rarity
    /// colour, lit on the top and left and shadowed on the bottom and right, a dark inner line, and a stud at each corner.
    /// </summary>
    private static void SlotFrame(Image box, Color? rarity)
    {
        if (edges[0] != null && edges[0].Sprite != null)
        {
            // the forge's own frame pieces, over the item, with only a thin line of the rarity colour just inside
            ForgeBorder(box.transform, null);
            float th = Mathf.Min(edges[0].Thickness, 12f);
            Color line = rarity ?? new Color(0.5f, 0.4f, 0.24f);
            line.a = 0.9f;
            Transform ft = box.transform;
            Part(ft, "RarityTop", line, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -th), new Vector2(-2 * th, 2));
            Part(ft, "RarityBottom", line, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, th), new Vector2(-2 * th, 2));
            Part(ft, "RarityLeft", line, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(th, 0), new Vector2(2, -2 * th));
            Part(ft, "RarityRight", line, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-th, 0), new Vector2(2, -2 * th));
            return;
        }
        Color c = rarity ?? new Color(0.50f, 0.40f, 0.24f);
        c = new Color(Mathf.Min(1f, c.r * 1.1f), Mathf.Min(1f, c.g * 1.1f), Mathf.Min(1f, c.b * 1.1f), 1f);
        Color lit = Color.Lerp(c, Color.white, 0.28f);
        Color dim = new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 1f);
        const float t = 5f;
        Transform tf = box.transform;
        Vector2 mid = new(0.5f, 0.5f);
        Part(tf, "RimTop", lit, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, t));
        Part(tf, "RimLeft", lit, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(t, 0));
        Part(tf, "RimBottom", dim, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, t));
        Part(tf, "RimRight", dim, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(t, 0));
        // a dark line just inside the rim, so the art sits in a recess
        Color shadow = new(0f, 0f, 0f, 0.7f);
        Part(tf, "InTop", shadow, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -t), new Vector2(-2 * t, 2));
        Part(tf, "InBottom", shadow, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, t), new Vector2(-2 * t, 2));
        Part(tf, "InLeft", shadow, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(t, 0), new Vector2(2, -2 * t));
        Part(tf, "InRight", shadow, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-t, 0), new Vector2(2, -2 * t));
        foreach (Vector2 corner in new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) })
        {
            Part(tf, "Stud", dim, corner, corner, mid, Vector2.zero, new Vector2(10, 10), 45f);
            Part(tf, "StudCore", lit, corner, corner, mid, Vector2.zero, new Vector2(5, 5), 45f);
        }
    }

    /// <summary>The forge's own frame pieces (its sprites), laid round a slot.</summary>
    private static void ForgeBorder(Transform parent, Color? rarity = null)
    {
        for (int side = 0; side < 4; side++) // top, bottom, left, right
        {
            EdgeSprite e = edges[side];
            if (e == null || e.Sprite == null)
            {
                continue;
            }
            Color edge = e.Color;
            if (rarity.HasValue)
            {
                Color rc = rarity.Value;
                edge = new Color(Mathf.Min(1f, rc.r * 1.3f), Mathf.Min(1f, rc.g * 1.3f), Mathf.Min(1f, rc.b * 1.3f), 1f);
            }
            Image img = MakeImage(parent, "Border", edge);
            img.sprite = e.Sprite;
            img.type = e.Type;
            img.raycastTarget = false;
            RectTransform r = img.rectTransform;
            float t = e.Thickness;
            switch (side)
            {
                case 0:
                    r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(0, t);
                    break;
                case 1:
                    r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(0.5f, 0); r.sizeDelta = new Vector2(0, t);
                    break;
                case 2:
                    r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 0.5f); r.sizeDelta = new Vector2(t, 0);
                    break;
                default:
                    r.anchorMin = new Vector2(1, 0); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 0.5f); r.sizeDelta = new Vector2(t, 0);
                    break;
            }
            r.anchoredPosition = Vector2.zero;
        }
    }

    private sealed class EdgeSprite
    {
        public Sprite Sprite;
        public Image.Type Type;
        public Color Color;
        public float Thickness;
    }

    private static readonly EdgeSprite[] edges = new EdgeSprite[4];
    private static Sprite dividerSprite;
    private static float dividerHeight = 2;

    /// <summary>Reads the forge's frame and divider sprites, once.</summary>
    private static void CaptureForgeArt(Transform main)
    {
        string[] names = { "Framing/border-piece-top", "Framing/border-piece-bottom", "Framing/border-piece-left", "Framing/border-piece-right" };
        for (int i = 0; i < 4; i++)
        {
            Transform t = main.Find(names[i]);
            Image img = t != null ? t.GetComponent<Image>() : null;
            if (img == null)
            {
                continue;
            }
            Vector2 size = img.rectTransform.sizeDelta;
            float thick = i < 2 ? Mathf.Abs(size.y) : Mathf.Abs(size.x);
            edges[i] = new EdgeSprite { Sprite = img.sprite, Type = img.type, Color = img.color, Thickness = Mathf.Clamp(thick > 0 ? thick : 10, 6, 12) };
        }
        Transform sep = main.Find("BottomVisuals/seperator");
        Image sepImg = sep != null ? sep.GetComponent<Image>() : null;
        if (sepImg != null)
        {
            dividerSprite = sepImg.sprite;
            dividerHeight = Mathf.Clamp(Mathf.Abs(sepImg.rectTransform.sizeDelta.y), 2, 8);
        }
    }

    private static void Divider(float x, float y, float w)
    {
        Image img = MakeImage(Content, "Divider", dividerSprite != null ? Color.white : new Color(0.5f, 0.4f, 0.22f, 0.8f));
        if (dividerSprite != null)
        {
            img.sprite = dividerSprite;
        }
        img.raycastTarget = false;
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, dividerSprite != null ? dividerHeight : 2);
        dynamic.Add(img.gameObject);
    }

    private static void Stretch(RectTransform r, float margin)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(margin, margin);
        r.offsetMax = new Vector2(-margin, -margin);
    }

    /// <summary>
    /// The item's icon, taken from the game's own item display: a copy of an inventory item widget is asked to
    /// draw this item, and the sprites it picks are kept. The copy is thrown away at once.
    /// </summary>
    private static Icon IconFor(ItemData item, int id)
    {
        long key = (long)item.Pointer;
        if (icons[id] != null && iconFor[id] == key)
        {
            return icons[id];
        }
        icons[id]?.Dispose();
        icons[id] = BuildIcon(item);
        iconFor[id] = key;
        return icons[id];
    }

    private static Icon BuildIcon(ItemData item)
    {
        try
        {
            // the same call the game's own item widgets make, loaded through our own handle
            SoftRef<Sprite> soft = item.GetItemSpriteFromData(ItemUIContext.Default);
            LoadRef<Sprite> load = soft == null || !soft ? null : SoftRefExtensions.CreateLoadRef(soft, "LastEpoch_Hud", 0);
            if (load == null)
            {
                Main.logger_instance?.Warning("Recombinator: no sprite reference for the item");
                return null;
            }
            var icon = new Icon { Load = load, Born = Time.unscaledTime };
            // the game's own background for this item (its rarity look): ask an inventory item widget to set one up
            try
            {
                InventoryItemUI source = null;
                foreach (InventoryItemUI ui in Object.FindObjectsOfType<InventoryItemUI>())
                {
                    if (ui != null && ui.gameObject.activeInHierarchy && !ui.backgroundImage.IsNullOrDestroyed())
                    {
                        source = ui;
                        break;
                    }
                }
                if (source != null)
                {
                    var holder = new GameObject("RecombinatorBackgroundHolder");
                    holder.AddComponent<RectTransform>();
                    holder.transform.SetParent(root.transform, false);
                    holder.SetActive(false); // nothing on the copy wakes up
                    GameObject clone = Object.Instantiate(source.gameObject, holder.transform, false);
                    InventoryItemUI copy = clone.GetComponent<InventoryItemUI>();
                    copy.SetBackgroundSpriteAndColour(copy.backgroundImage, item);
                    icon.Holder = holder;
                    icon.Copy = copy;
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Recombinator background failed: " + ex.Message);
            }
            icon.Poll();
            return icon;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Recombinator icon failed: " + ex.Message);
            return null;
        }
    }

    /// <summary>Re-draws the window when an icon that was still loading has its sprite.</summary>
    private static void TickIcons()
    {
        for (int i = 0; i < icons.Length; i++)
        {
            Icon icon = icons[i];
            if (icon != null && icon.Content == null && icon.Poll())
            {
                dirty = true; // the sprite has arrived: draw it
            }
        }
        // if the result's own icon never loads, it takes the look of the item it was made from
        Icon res = icons[2];
        if (res != null && resultRevealed && res.Content == null && Time.unscaledTime - res.Born > 3f)
        {
            Icon from = icons[0]?.Content != null ? icons[0] : icons[1]?.Content != null ? icons[1] : null;
            if (from != null)
            {
                res.Content = from.Content;
                dirty = true;
            }
        }
    }

    private static void TickHover()
    {
        int now = -1;
        Vector2 mouse = Input.mousePosition;
        UnityEngine.Camera cam = null;
        Canvas canvas = root.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = canvas.worldCamera;
        }
        for (int i = 0; i < slotRects.Length; i++)
        {
            RectTransform r = slotRects[i];
            if (r != null && slotItems[i] != null && RectTransformUtility.RectangleContainsScreenPoint(r, mouse, cam))
            {
                now = i;
                break;
            }
        }
        if (now == hovered)
        {
            return;
        }
        CloseTooltip();
        hovered = now;
        if (now >= 0)
        {
            try
            {
                TooltipItemManager manager = Object.FindObjectOfType<TooltipItemManager>();
                ItemDataUnpacked data = slotItems[now].TryCast<ItemDataUnpacked>();
                RectTransform r = slotRects[now];
                if (manager != null && data != null)
                {
                    manager.OpenTooltip(data, TooltipItemManager.SlotType.CRAFTING, Vector2.zero, r.position, r.gameObject, r.rect.size);
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Recombinator tooltip failed: " + ex.Message);
            }
        }
    }

    private static void CloseTooltip()
    {
        if (hovered < 0)
        {
            return;
        }
        hovered = -1;
        try
        {
            TooltipItemManager.ForceClose();
        }
        catch (Exception)
        {
        }
    }

    private static Vector2 Sample(List<Vector2> pts, float distance)
    {
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            float len = Vector2.Distance(pts[i], pts[i + 1]);
            if (distance <= len || i + 2 == pts.Count)
            {
                return Vector2.Lerp(pts[i], pts[i + 1], len <= 0 ? 0 : Mathf.Clamp01(distance / len));
            }
            distance -= len;
        }
        return pts[pts.Count - 1];
    }

    private static float PathLength(List<Vector2> pts)
    {
        float total = 0;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            total += Vector2.Distance(pts[i], pts[i + 1]);
        }
        return total;
    }

    /// <summary>Soul embers leave both items, run along the trenches and sink into the result.</summary>
    private static void StartPour(bool crushBones)
    {
        FindCurrencyIcons();
        pourBones = crushBones;
        pourTotal = crushBones ? 3.7f : 2.7f;
        chargeDone = false;
        pouring = true;
        pourStart = Time.unscaledTime;
        for (int side = 0; side < 2; side++)
        {
            // the path in screen coordinates (y up): the pieces are laid out with y down
            var pts = new List<Vector2>();
            foreach (Vector2 q in TrenchPoints(side, true))
            {
                pts.Add(new Vector2(q.x, -q.y));
            }
            pourPath[side] = pts;
            pourLength[side] = PathLength(pts);
            for (int n = 0; n < 44; n++)
            {
                SpawnEmber(side, pourStart + n * 0.032f + side * 0.015f, UnityEngine.Random.Range(0.75f, 1.1f));
            }
        }
        resultRevealed = false;
        revealAt = pourStart + pourTotal;
        if (crushBones)
        {
            // three bones drop into the result and are crushed once the embers have gathered
            Vector2 slotCentre = new(CenterX, -(ResY + IconH / 2));
            for (int i = 0; i < 3; i++)
            {
                float dx = (i - 1) * 46;
                Piece bone = NewPiece(true, new Vector2(CenterX + dx, -(RunY + 10)), slotCentre + new Vector2(dx * 0.25f, 0), pourStart + 1.8f + i * 0.14f, 0.5f);
                bone.Spin = (i - 1) * 140;
            }
        }
    }

    private static void SpawnEmber(int side, float start, float duration)
    {
        float mixT = UnityEngine.Random.value; // each ember is somewhere between orange and green
        Color emberTint = Color.Lerp(new Color(1f, 0.5f, 0.12f, 0.5f), new Color(0.45f, 0.95f, 0.25f, 0.5f), mixT);
        Image glow = MakeImage(Content, "Ember", emberTint);
        glow.raycastTarget = false;
        RectTransform r = glow.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0.5f, 0.5f);
        float emberSize = UnityEngine.Random.Range(12f, 30f);
        r.sizeDelta = new Vector2(emberSize, emberSize * UnityEngine.Random.Range(0.65f, 1.35f));
        r.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(15f, 75f));
        Image core = MakeImage(glow.transform, "Core", Color.Lerp(new Color(1f, 0.93f, 0.62f, 1f), new Color(0.88f, 1f, 0.62f, 1f), mixT));
        core.raycastTarget = false;
        RectTransform cr = core.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
        cr.sizeDelta = new Vector2(10, 10);
        CanvasGroup group = glow.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0;
        ghosts.Add(new Ghost { Go = glow.gameObject, Rect = r, Group = group, Side = side, Start = start, Duration = duration });
    }

    private static void TickAnimations()
    {
        float now = Time.unscaledTime;
        TickPieces(now);
        TickCage(now);
        TickCracks(now);
        if (pouring)
        {
            float t = Mathf.Clamp01((now - pourStart) / pourTotal);
            float fade = Mathf.Lerp(1f, 0.25f, t);
            for (int i = 0; i < 2; i++)
            {
                if (slotGroups[i] != null)
                {
                    slotGroups[i].alpha = fade;
                }
            }
            float ramp = Mathf.Clamp01(t / 0.7f); // the wave front, 0 at the item boxes to 1 at the result
            float breathe = 0.25f + 0.2f * Mathf.Sin(now * 22f);
            for (int k = 0; k < trenchInner.Count; k++)
            {
                Image img = trenchInner[k];
                if (img == null)
                {
                    continue;
                }
                float behind = ramp * 1.15f - trenchAlong[k];
                float glow = behind <= 0 ? 0f : Mathf.Exp(-behind * 2.2f); // bright at the front, trailing off behind it
                glow = Mathf.Clamp01(glow + (behind > 0 ? breathe * 0.35f : 0f));
                // bands of orange and green flow along the pipe with the embers
                float band = 0.5f + 0.5f * Mathf.Sin(trenchAlong[k] * 16f - now * 7f);
                Color flow = Color.Lerp(TrenchGlow, new Color(0.5f, 0.95f, 0.25f), band);
                Color hot = Color.Lerp(flow, new Color(1f, 0.97f, 0.75f), Mathf.Clamp01(glow * glow) * 0.55f);
                img.color = Color.Lerp(trenchBase[k], hot, glow);
            }
            if (!chargeDone && now >= pourStart + pourTotal - 0.75f)
            {
                chargeDone = true;
                Vector2 centre = new(CenterX, -(ResY + IconH / 2));
                for (int k = 0; k < 40; k++)
                {
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2);
                    float dist = UnityEngine.Random.Range(150f, 280f);
                    NewSpark(centre + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist, centre, now + UnityEngine.Random.Range(0f, 0.25f),
                        UnityEngine.Random.Range(0.4f, 0.65f), RandomFire(), UnityEngine.Random.Range(4f, 9f), inward: true);
                }
            }
            if (now >= pourStart + pourTotal)
            {
                pouring = false;
                cageReleaseAt = now;
                ResetTunnel();
                Array.Clear(pourItems, 0, 2);
                resultRevealed = resultItem != null;
                pendingFlash = resultRevealed;
                dirty = true;
            }
        }
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            Ghost g = ghosts[i];
            if (g.Go == null)
            {
                ghosts.RemoveAt(i);
                continue;
            }
            if (now < g.Start)
            {
                continue;
            }
            float p = Mathf.Clamp01((now - g.Start) / g.Duration);
            List<Vector2> path = pourPath[g.Side];
            g.Rect.anchoredPosition = Sample(path, p * pourLength[g.Side]);
            float size = 0.7f + 0.5f * Mathf.Sin(p * Mathf.PI);
            g.Rect.localScale = new Vector3(size, size, 1);
            g.Group.alpha = p < 0.1f ? p / 0.1f : p > 0.85f ? (1f - p) / 0.15f : 1f;
            if (p >= 1f)
            {
                Object.Destroy(g.Go);
                ghosts.RemoveAt(i);
            }
        }
        for (int i = flashes.Count - 1; i >= 0; i--)
        {
            Flash f = flashes[i];
            float p = (now - f.Start) / 0.95f;
            if (p >= 1f || f.Glow == null)
            {
                DestroyFlash(f);
                flashes.RemoveAt(i);
                continue;
            }
            float inv = 1f - p;
            Color o = f.Overlay.color;
            o.a = 0.95f * inv * inv;
            f.Overlay.color = o;
            Color g2 = f.Glow.color;
            g2.a = 0.8f * inv;
            f.Glow.color = g2;
            f.Glow.rectTransform.localScale = Vector3.one * (1f + 1.4f * p);
            if (f.Wash != null)
            {
                Color w = f.Wash.color;
                w.a = 0.3f * Mathf.Pow(Mathf.Clamp01(1f - p * 3.2f), 2f);
                f.Wash.color = w;
            }
            for (int k = 0; k < f.Rays.Count; k++)
            {
                Image ray = f.Rays[k];
                if (ray == null)
                {
                    continue;
                }
                Color rc = ray.color;
                rc.a = 0.6f * inv * inv;
                ray.color = rc;
                ray.rectTransform.localScale = new Vector3(1f - 0.6f * p, 0.35f + 1.5f * p, 1f);
                ray.rectTransform.localRotation = Quaternion.Euler(0, 0, k * (360f / f.Rays.Count) + 55f * p);
            }
        }

        // the whole window trembles as the embers gather, harder when the bones are crushed and the result appears
        if (contentRect != null)
        {
            float amp = 0f;
            if (pouring)
            {
                amp = Mathf.Max(amp, Mathf.Clamp01(((now - pourStart) / pourTotal - 0.2f) / 0.6f) * 3.5f);
            }
            float sinceCrush = now - crushAt;
            if (sinceCrush >= 0 && sinceCrush < 0.5f)
            {
                amp = Mathf.Max(amp, 9f * (1f - sinceCrush / 0.5f));
            }
            float sinceFlash = now - flashAt;
            if (sinceFlash >= 0 && sinceFlash < 0.8f)
            {
                amp = Mathf.Max(amp, 17f * (1f - sinceFlash / 0.8f));
            }
            contentRect.anchoredPosition = amp > 0.05f
                ? contentBase + new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * amp
                : contentBase;
        }
    }

    private static void ResetTunnel()
    {
        for (int k = 0; k < trenchInner.Count; k++)
        {
            if (trenchInner[k] != null)
            {
                trenchInner[k].color = trenchBase[k];
            }
        }
    }

    private sealed class CrackSeg
    {
        public Image Glow, Dark;
        public float At;
    }

    private static readonly List<CrackSeg> cracks = new();
    private static Image crackWash;
    private static float crackStart;

    private static void AddCrack(Vector2 from, Vector2 to, float at)
    {
        Vector2 mid = (from + to) / 2;
        Vector2 d = to - from;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        Image glow = MakeImage(Content, "CrackGlow", new Color(1f, 0.42f, 0.18f, 0f));
        Image dark = MakeImage(Content, "Crack", new Color(0.03f, 0.02f, 0.02f, 0f));
        foreach ((Image img, float thick) in new[] { (glow, 7f), (dark, 2.6f) })
        {
            img.raycastTarget = false;
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = mid;
            r.sizeDelta = new Vector2(d.magnitude + 2, thick);
            r.localRotation = Quaternion.Euler(0, 0, angle);
        }
        cracks.Add(new CrackSeg { Glow = glow, Dark = dark, At = at });
    }

    /// <summary>The recombination failed: the result is shown as it is, cracking across its box.</summary>
    private static void SpawnCrack()
    {
        Vector2 centre = new(CenterX, -(ResY + IconH / 2));
        float now = Time.unscaledTime;
        crackStart = now;
        crushAt = now; // the same short jolt as a crushed bone
        float halfW = IconW / 2f - 3, halfH = IconH / 2f - 3;
        int rays = 9;
        for (int k = 0; k < rays; k++)
        {
            float angle = k * Mathf.PI * 2 / rays + UnityEngine.Random.Range(-0.25f, 0.25f);
            Vector2 pos = centre;
            for (int step = 0; step < 6; step++)
            {
                float len = UnityEngine.Random.Range(16f, 30f);
                angle += UnityEngine.Random.Range(-0.5f, 0.5f);
                Vector2 next = pos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * len;
                next.x = Mathf.Clamp(next.x, centre.x - halfW, centre.x + halfW);
                next.y = Mathf.Clamp(next.y, centre.y - halfH, centre.y + halfH);
                AddCrack(pos, next, now + k * 0.02f + step * 0.05f);
                if (step == 2 && UnityEngine.Random.value < 0.6f)
                {
                    float branch = angle + (UnityEngine.Random.value < 0.5f ? -1f : 1f) * UnityEngine.Random.Range(0.6f, 1.1f);
                    Vector2 end = next + new Vector2(Mathf.Cos(branch), Mathf.Sin(branch)) * UnityEngine.Random.Range(20f, 36f);
                    end.x = Mathf.Clamp(end.x, centre.x - halfW, centre.x + halfW);
                    end.y = Mathf.Clamp(end.y, centre.y - halfH, centre.y + halfH);
                    AddCrack(next, end, now + k * 0.02f + (step + 1) * 0.05f);
                }
                pos = next;
            }
        }
        crackWash = MakeImage(Content, "CrackWash", new Color(0.85f, 0.25f, 0.1f, 0.4f));
        crackWash.raycastTarget = false;
        RectTransform wr = crackWash.rectTransform;
        wr.anchorMin = wr.anchorMax = new Vector2(0, 1);
        wr.pivot = new Vector2(0.5f, 0.5f);
        wr.sizeDelta = new Vector2(IconW, IconH);
        wr.anchoredPosition = centre;
        // grey flakes shake loose
        for (int k = 0; k < 18; k++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            float speed = UnityEngine.Random.Range(60f, 220f);
            Piece flake = NewSpark(centre, centre, now + 0.1f, UnityEngine.Random.Range(0.5f, 1f), new Color(0.55f, 0.5f, 0.45f), UnityEngine.Random.Range(3f, 6f));
            flake.Velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed * 0.6f);
            flake.Gravity = 520f;
            flake.Spin = UnityEngine.Random.Range(-300f, 300f);
        }
    }

    private static void TickCracks(float now)
    {
        if (cracks.Count == 0 && crackWash == null)
        {
            return;
        }
        float age = now - crackStart;
        float fade = age < 2.2f ? 1f : Mathf.Clamp01(1f - (age - 2.2f) / 0.6f);
        foreach (CrackSeg c in cracks)
        {
            if (c.Dark == null)
            {
                continue;
            }
            float on = now >= c.At ? 1f : 0f;
            Color d = c.Dark.color;
            d.a = 0.95f * on * fade;
            c.Dark.color = d;
            Color g = c.Glow.color;
            g.a = 0.55f * on * fade * (0.7f + 0.3f * Mathf.Sin(now * 14f));
            c.Glow.color = g;
        }
        if (crackWash != null)
        {
            Color w = crackWash.color;
            w.a = 0.4f * Mathf.Pow(Mathf.Clamp01(1f - age / 0.5f), 2f);
            crackWash.color = w;
        }
        if (age > 2.9f)
        {
            ClearCracks();
        }
    }

    private static void ClearCracks()
    {
        foreach (CrackSeg c in cracks)
        {
            if (c.Glow != null) Object.Destroy(c.Glow.gameObject);
            if (c.Dark != null) Object.Destroy(c.Dark.gameObject);
        }
        cracks.Clear();
        if (crackWash != null)
        {
            Object.Destroy(crackWash.gameObject);
            crackWash = null;
        }
    }

    private static void DestroyFlash(Flash f)
    {
        if (f.Glow != null) Object.Destroy(f.Glow.gameObject);
        if (f.Overlay != null) Object.Destroy(f.Overlay.gameObject);
        if (f.Wash != null) Object.Destroy(f.Wash.gameObject);
        foreach (Image ray in f.Rays)
        {
            if (ray != null) Object.Destroy(ray.gameObject);
        }
    }

    private static Color SoulLight() =>
        Color.Lerp(new Color(0.3f, 0.8f, 1f), new Color(0.95f, 1f, 1f), UnityEngine.Random.value);

    /// <summary>Soul ember fire: an orange and green mix, with some pale heat.</summary>
    private static Color RandomFire() =>
        Color.Lerp(Color.Lerp(new Color(1f, 0.45f, 0.1f), new Color(0.5f, 0.95f, 0.25f), UnityEngine.Random.value), new Color(1f, 0.97f, 0.75f), UnityEngine.Random.value * 0.35f);

    /// <summary>A bright spark: flies out and falls, or (inward) streams in to a point.</summary>
    private static Piece NewSpark(Vector2 from, Vector2 to, float start, float life, Color color, float size, bool inward = false)
    {
        color.a *= UnityEngine.Random.Range(0.6f, 1f);
        Image img = MakeImage(Content, "Spark", color);
        img.raycastTarget = false;
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(size, size * (inward ? 1f : UnityEngine.Random.Range(1f, 2.4f)));
        r.anchoredPosition = from;
        r.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 90f));
        CanvasGroup group = img.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0;
        var piece = new Piece { Go = img.gameObject, Rect = r, Group = group, IsDrop = inward, Inward = inward, From = from, To = to, Start = start, Life = life };
        pieces.Add(piece);
        return piece;
    }

    /// <summary>A burst of sparks from a point.</summary>
    private static void SparkBurst(Vector2 at, int count, float minSpeed, float maxSpeed, float gravity, float minLife, float maxLife)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            float speed = UnityEngine.Random.Range(minSpeed, maxSpeed);
            Piece spark = NewSpark(at, at, Time.unscaledTime, UnityEngine.Random.Range(minLife, maxLife), RandomFire(), UnityEngine.Random.Range(3f, 7f));
            spark.Velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);
            spark.Gravity = gravity;
            spark.Spin = UnityEngine.Random.Range(-720f, 720f);
        }
    }

    /// <summary>The final flash: a white wash, bright rays, a swelling glow, a ring of sparks and a shower.</summary>
    private static void SpawnFlash()
    {
        Vector2 centre = new(CenterX, -(ResY + IconH / 2));
        flashAt = Time.unscaledTime;

        Image wash = MakeImage(Content, "FlashWash", new Color(1f, 0.88f, 0.62f, 0.3f));
        wash.raycastTarget = false;
        RectTransform wr = wash.rectTransform;
        wr.anchorMin = Vector2.zero;
        wr.anchorMax = Vector2.one;
        wr.offsetMin = wr.offsetMax = Vector2.zero;

        var flash = new Flash { Start = Time.unscaledTime, Wash = wash };
        for (int k = 0; k < 14; k++)
        {
            Image ray = MakeImage(Content, "FlashRay", k % 3 == 1 ? new Color(0.62f, 1f, 0.42f, 0.85f) : new Color(1f, 0.82f, 0.45f, 0.85f));
            ray.raycastTarget = false;
            RectTransform rr = ray.rectTransform;
            rr.anchorMin = rr.anchorMax = new Vector2(0, 1);
            rr.pivot = new Vector2(0.5f, 0f);
            rr.sizeDelta = new Vector2(UnityEngine.Random.Range(2.5f, 9f), UnityEngine.Random.Range(230f, 440f));
            rr.anchoredPosition = centre;
            flash.Rays.Add(ray);
        }
        Image glow = MakeImage(Content, "FlashGlow", new Color(1f, 0.82f, 0.4f, 0.8f));
        Image overlay = MakeImage(Content, "FlashOverlay", new Color(1f, 0.88f, 0.6f, 0.8f));
        foreach ((Image img, Vector2 size) in new[] { (glow, new Vector2(IconW + 90, IconH + 90)), (overlay, new Vector2(IconW, IconH)) })
        {
            img.raycastTarget = false;
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size;
            r.anchoredPosition = centre;
        }
        flash.Glow = glow;
        flash.Overlay = overlay;
        flashes.Add(flash);

        // a ring of sparks racing outward, then a shower
        for (int k = 0; k < 36; k++)
        {
            float angle = k * Mathf.PI * 2 / 36;
            Piece ring = NewSpark(centre, centre, Time.unscaledTime, 0.6f, new Color(1f, 0.93f, 0.65f), 6f);
            ring.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 640f;
            ring.Spin = 360f;
        }
        SparkBurst(centre, 90, 160f, 760f, 520f, 0.7f, 1.5f);
    }

    private static Piece NewPiece(bool drop, Vector2 from, Vector2 to, float start, float life)
    {
        Color bone = new(0.93f, 0.90f, 0.79f, 1f);
        Image img = MakeImage(Content, drop ? "Bone" : "Shard", bone);
        img.raycastTarget = false;
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0.5f, 0.5f);
        if (drop && bonesIcon != null)
        {
            img.sprite = bonesIcon;
            img.preserveAspect = true;
            img.color = Color.white;
            r.sizeDelta = new Vector2(46, 46);
        }
        else if (drop)
        {
            r.sizeDelta = new Vector2(12, 40); // a plain bone shaft when the icon is not found
        }
        else
        {
            r.sizeDelta = new Vector2(UnityEngine.Random.Range(5f, 9f), UnityEngine.Random.Range(10f, 20f));
        }
        r.anchoredPosition = from;
        CanvasGroup group = img.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0;
        var piece = new Piece { Go = img.gameObject, Rect = r, Group = group, IsDrop = drop, From = from, To = to, Start = start, Life = life };
        pieces.Add(piece);
        return piece;
    }

    /// <summary>The bone is crushed: it bursts into shards that fly out, fall and fade.</summary>
    private static void Crush(Vector2 at)
    {
        crushAt = Time.unscaledTime;
        SparkBurst(at, 26, 140f, 520f, 700f, 0.4f, 0.9f);
        for (int i = 0; i < 24; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            float speed = UnityEngine.Random.Range(160f, 460f);
            Piece shard = NewPiece(false, at, at, Time.unscaledTime, UnityEngine.Random.Range(0.55f, 0.9f));
            shard.Velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed + 90f);
            shard.Gravity = 620f;
            shard.Spin = UnityEngine.Random.Range(-540f, 540f);
            shard.Rect.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 180f));
        }
    }

    private static void TickPieces(float now)
    {
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            Piece pc = pieces[i];
            if (pc.Go == null)
            {
                pieces.RemoveAt(i);
                continue;
            }
            if (now < pc.Start)
            {
                continue;
            }
            float age = now - pc.Start;
            if (pc.IsDrop)
            {
                float p = Mathf.Clamp01(age / pc.Life);
                pc.Rect.anchoredPosition = Vector2.Lerp(pc.From, pc.To, p * p);
                pc.Rect.localRotation = Quaternion.Euler(0, 0, pc.Spin * p);
                float scale = pc.Inward ? Mathf.Lerp(1.3f, 0.3f, p) : Mathf.Lerp(1.9f, 0.9f, p);
                pc.Rect.localScale = new Vector3(scale, scale, 1);
                pc.Group.alpha = pc.Inward ? Mathf.Clamp01(p * 8f) * Mathf.Clamp01((1f - p) * 4f) : Mathf.Clamp01(p * 6f);
                if (p >= 1f)
                {
                    Vector2 at = pc.To;
                    Object.Destroy(pc.Go);
                    pieces.RemoveAt(i);
                    if (!pc.Inward)
                    {
                        Crush(at);
                    }
                }
            }
            else
            {
                float p = age / pc.Life;
                Vector2 pos = pc.From + pc.Velocity * age;
                pos.y -= 0.5f * pc.Gravity * age * age;
                pc.Rect.anchoredPosition = pos;
                pc.Rect.Rotate(0, 0, pc.Spin * Time.unscaledDeltaTime);
                pc.Group.alpha = Mathf.Clamp01(1f - p);
                if (p >= 1f)
                {
                    Object.Destroy(pc.Go);
                    pieces.RemoveAt(i);
                }
            }
        }

        // the result box shakes while the bones are crushed
        float since = now - crushAt;
        RectTransform box = slotBoxes[2];
        if (box != null)
        {
            if (since >= 0 && since < 0.55f)
            {
                float k = 1f - since / 0.55f;
                box.anchoredPosition = slotBoxBase[2] + new Vector2(UnityEngine.Random.Range(-7f, 7f), UnityEngine.Random.Range(-5f, 5f)) * k;
            }
            else if (since >= 0.55f && since < 0.7f)
            {
                box.anchoredPosition = slotBoxBase[2];
            }
        }
    }

    private static void ClearGhosts()
    {
        foreach (Piece pc in pieces)
        {
            if (pc.Go != null)
            {
                Object.Destroy(pc.Go);
            }
        }
        pieces.Clear();
        crushAt = -10f;
        foreach (Ghost g in ghosts)
        {
            if (g.Go != null)
            {
                Object.Destroy(g.Go);
            }
        }
        ghosts.Clear();
        foreach (Flash f in flashes)
        {
            DestroyFlash(f);
        }
        flashes.Clear();
        ClearCracks();
        if (contentRect != null)
        {
            contentRect.anchoredPosition = contentBase;
        }
    }

    private static void TakeResult()
    {
        if (resultItem == null || !resultRevealed)
        {
            return;
        }
        GiveBack(resultItem);
        resultItem = null;
        resultRevealed = false;
        icons[2]?.Dispose();
        icons[2] = null;
        message = "";
        dirty = true;
    }

    private sealed class Row
    {
        public RectTransform Rect;
        public Image Bg, Bar;
        public Image[] Edge = new Image[4];
        public TextMeshProUGUI Name, Tier;
        public Color TierColor;
        public bool On, Hover, Static;
    }

    private static readonly List<Row> rows = new();
    private static readonly Color TierGrey = new(0.52f, 0.50f, 0.47f);
    private static readonly Color TierGreyLit = new(0.72f, 0.70f, 0.66f);
    private static readonly Color RowBase = new(0.035f, 0.03f, 0.026f, 0.82f);
    private static readonly Color RowHover = new(0.085f, 0.07f, 0.05f, 0.9f);
    private static readonly Color RowOn = new(0.13f, 0.10f, 0.055f, 0.94f);
    private static readonly Color RowOnHover = new(0.17f, 0.13f, 0.065f, 0.96f);
    private static readonly Color EdgeBase = new(0.62f, 0.56f, 0.48f, 1f);
    private static readonly Color EdgeHover = new(0.9f, 0.8f, 0.6f, 1f);
    private static readonly Color EdgeOn = new(1f, 0.82f, 0.4f, 1f);

    private sealed class RowInfo
    {
        public RecombAffix Affix;
        public string Text;
        public int Lines;
    }

    private static TextMeshProUGUI RowText(Transform parent, TextAlignmentOptions align, float size, float left, float right)
    {
        GameObject go = Object.Instantiate(labelTemplate, parent, false);
        StripGameLogic(go);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(left, 0);
        r.offsetMax = new Vector2(-right, 0);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (bodyFont != null)
        {
            t.font = bodyFont;
        }
        t.alignment = align;
        t.fontSize = size;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        go.SetActive(true);
        return t;
    }

    private static readonly Regex ColourTags = new("</?color[^>]*>|</?link[^>]*>", RegexOptions.Compiled);

    /// <summary>The affix worded the way the item's own tooltip words it, with its roll. May run over several lines.</summary>
    private static int loggedAffixes;
    private static readonly Regex LineBreaks = new(@"<br\s*/?>|\r\n|\r| | |\v|\\n", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string ItemWording(RecombAffix a)
    {
        try
        {
            ItemData owner = a.Source == 0 ? offer.A : offer.B;
            ItemAffix affix = owner.affixes.ToArray().FirstOrDefault(x => x != null && !x.IsSealed && x.affixId == a.AffixId);
            ItemDataUnpacked data = owner.TryCast<ItemDataUnpacked>();
            if (affix != null && data != null)
            {
                string text = TooltipItemManager.FormatAffix(data, affix, TooltipMode.DefaultInfo, TooltipItemManager.SlotType.CRAFTING,
                    false, -1, -1, false, false, ValueRangeFormat.None, true, false);
                if (loggedAffixes < 12)
                {
                    loggedAffixes++;
                    Main.logger_instance?.Msg("Recombinator affix text: " + (text ?? "null").Replace("\n", "<LF>").Replace("\r", "<CR>"));
                }
                text = ColourTags.Replace(text ?? "", "");
                // whatever the game uses to break a line becomes a real line break
                text = LineBreaks.Replace(text, "\n");
                string[] lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
                if (lines.Length > 0)
                {
                    return string.Join("\n", lines);
                }
            }
        }
        catch (Exception)
        {
        }
        AffixList.Affix def = AffixList.get()?.GetAffix(a.AffixId);
        return def == null ? "Affix " + a.AffixId : NativeItemNames.AffixName(def);
    }

    private static Color TierColour(int tier) => tier switch
    {
        <= 1 => new Color(0.66f, 0.65f, 0.62f),
        2 => new Color(0.62f, 0.74f, 0.64f),
        3 => new Color(0.50f, 0.80f, 0.55f),
        4 => new Color(0.48f, 0.68f, 1f),
        5 => new Color(0.70f, 0.56f, 1f),
        6 => new Color(0.90f, 0.52f, 0.92f),
        _ => new Color(1f, 0.66f, 0.28f),
    };

    internal static Image Part(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, float rotation = 0f)
    {
        Image img = MakeImage(parent, name, color);
        img.raycastTarget = false;
        RectTransform r = img.rectTransform;
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        r.localRotation = Quaternion.Euler(0, 0, rotation);
        return img;
    }

    /// <summary>Lays out the column: each plate is as tall as its lines need, and the whole stack stays inside its space.</summary>
    private const float ColumnWidth = 226;

    private static void ListAffixes(List<RecombAffix> list, float x, float y)
    {
        const float width = ColumnWidth, gap = 4, space = IconH, chars = 25;
        var infos = new List<RowInfo>();
        foreach (RecombAffix a in list)
        {
            string text = ItemWording(a);
            if (!(a.LegalOnA && a.LegalOnB))
            {
                text += "\n<size=75%>(fits one base only)</size>";
            }
            int lines = 0;
            foreach (string l in text.Split('\n'))
            {
                lines += Mathf.Max(1, Mathf.CeilToInt(l.Length / chars));
            }
            infos.Add(new RowInfo { Affix = a, Text = text, Lines = Mathf.Max(1, lines) });
        }
        if (infos.Count == 0)
        {
            return;
        }
        float units = infos.Sum(i => i.Lines);
        float perLine = Mathf.Min(30f, (space - (infos.Count - 1) * gap - infos.Count * 10f) / units);
        float fontSize = Mathf.Clamp(perLine - 5f, 9f, 14f);
        float cursor = y;
        foreach (RowInfo info in infos)
        {
            float h = info.Lines * perLine + 10f;
            AffixRow(info.Affix, info.Text, x, cursor, width, h, fontSize, deterministic && selected.Contains(Key(info.Affix)), deterministic);
            cursor += h + gap;
        }
    }

    /// <summary>
    /// An affix plate in the forge's own manner: a flat dark plate, the forge's metal border, a small tier label and
    /// the affix text in the game's font. It warms and brightens under the mouse and takes a gold border when chosen.
    /// </summary>
    private static void AffixRow(RecombAffix a, string text, float x, float y, float w, float h, float fontSize, bool on, bool interactive)
    {
        Image bg = MakeImage(Content, "AffixRow", RowBase);
        RectTransform r = bg.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        dynamic.Add(bg.gameObject);

        var row = new Row { Rect = r, Bg = bg, On = on, Static = !interactive, TierColor = TierColour(a.DisplayedTier) };

        // the forge's frame pieces, thinned for a plate
        for (int side = 0; side < 4; side++)
        {
            EdgeSprite e = edges[side];
            Image img = MakeImage(bg.transform, "Edge", EdgeBase);
            img.raycastTarget = false;
            if (e != null && e.Sprite != null)
            {
                img.sprite = e.Sprite;
                img.type = e.Type;
            }
            RectTransform er = img.rectTransform;
            float t = e != null ? Mathf.Min(e.Thickness, 4f) : 2f;
            switch (side)
            {
                case 0: er.anchorMin = new Vector2(0, 1); er.anchorMax = new Vector2(1, 1); er.pivot = new Vector2(0.5f, 1); er.sizeDelta = new Vector2(0, t); break;
                case 1: er.anchorMin = new Vector2(0, 0); er.anchorMax = new Vector2(1, 0); er.pivot = new Vector2(0.5f, 0); er.sizeDelta = new Vector2(0, t); break;
                case 2: er.anchorMin = new Vector2(0, 0); er.anchorMax = new Vector2(0, 1); er.pivot = new Vector2(0, 0.5f); er.sizeDelta = new Vector2(t, 0); break;
                default: er.anchorMin = new Vector2(1, 0); er.anchorMax = new Vector2(1, 1); er.pivot = new Vector2(1, 0.5f); er.sizeDelta = new Vector2(t, 0); break;
            }
            er.anchoredPosition = Vector2.zero;
            row.Edge[side] = img;
        }

        // a thin gold mark down the left, bright when chosen
        row.Bar = Part(bg.transform, "Bar", EdgeOn, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(3, -10));
        row.Bar.color = new Color(EdgeOn.r, EdgeOn.g, EdgeOn.b, 0f);

        // the tier, small, in its colour, with a thin divider
        TextMeshProUGUI tier = RowText(bg.transform, TextAlignmentOptions.Center, 8, 8, 0);
        RectTransform tr = tier.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 0);
        tr.anchorMax = new Vector2(0, 1);
        tr.pivot = new Vector2(0, 0.5f);
        tr.offsetMin = new Vector2(8, 0);
        tr.offsetMax = new Vector2(34, 0);
        tier.overflowMode = TextOverflowModes.Overflow;
        tier.fontStyle = FontStyles.UpperCase; // the forge title style is lower case; a tier is always "T4"
        tier.text = "T" + a.DisplayedTier;
        tier.color = TierGrey;
        row.Tier = tier;
        Part(bg.transform, "Divider", new Color(0.55f, 0.46f, 0.32f, 0.55f), new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(38, 0), new Vector2(1, -12));

        // the affix as the item shows it, over as many lines as it needs
        TextMeshProUGUI name = RowText(bg.transform, TextAlignmentOptions.MidlineLeft, fontSize, 46, 10);
        name.enableWordWrapping = true;
        name.overflowMode = TextOverflowModes.Overflow;
        name.enableAutoSizing = true; // shrinks to fit the plate rather than being cut off
        name.fontSizeMin = 8;
        name.fontSizeMax = fontSize;
        name.fontStyle = FontStyles.Normal;
        name.text = text;
        row.Name = name;

        if (interactive)
        {
            Button button = bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener((Action)(() => Toggle(a)));
        }
        else
        {
            bg.raycastTarget = false;
        }
        ApplyRow(row, 1f);
        rows.Add(row);
    }

    private static void ApplyRow(Row row, float blend)
    {
        Color bg = row.On ? (row.Hover ? RowOnHover : RowOn) : row.Hover ? RowHover : RowBase;
        Color edge = row.On ? EdgeOn : row.Hover ? EdgeHover : EdgeBase;
        Color text = row.On ? new Color(1f, 0.93f, 0.68f) : row.Hover ? new Color(0.98f, 0.95f, 0.88f) : new Color(0.82f, 0.79f, 0.72f);
        row.Bg.color = Color.Lerp(row.Bg.color, bg, blend);
        foreach (Image e in row.Edge)
        {
            if (e != null)
            {
                e.color = Color.Lerp(e.color, edge, blend);
            }
        }
        row.Name.color = Color.Lerp(row.Name.color, text, blend);
        row.Tier.color = Color.Lerp(row.Tier.color, row.On || row.Hover ? TierGreyLit : TierGrey, blend);
        Color bar = row.Bar.color;
        bar.a = Mathf.Lerp(bar.a, row.On ? 1f : row.Hover ? 0.45f : 0f, blend);
        row.Bar.color = bar;
    }

    private static void TickRows()
    {
        if (rows.Count == 0)
        {
            return;
        }
        Vector2 mouse = Input.mousePosition;
        UnityEngine.Camera cam = null;
        Canvas canvas = root.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = canvas.worldCamera;
        }
        float blend = Mathf.Clamp01(Time.unscaledDeltaTime * 16f);
        foreach (Row row in rows)
        {
            if (row.Rect == null)
            {
                continue;
            }
            row.Hover = !row.Static && RectTransformUtility.RectangleContainsScreenPoint(row.Rect, mouse, cam);
            ApplyRow(row, blend);
        }
    }

    // ---------------------------------------------------------------- actions

    private static void SetMode(bool det)
    {
        deterministic = det;
        message = "";
        dirty = true;
    }

    private static string Key(RecombAffix a) => a.Source + ":" + a.AffixId;

    private static void Toggle(RecombAffix a)
    {
        if (!deterministic)
        {
            return;
        }
        string key = Key(a);
        if (!selected.Remove(key))
        {
            selected.Add(key);
        }
        message = "";
        dirty = true;
    }

    private static List<RecombAffix> Picks()
    {
        var result = new List<RecombAffix>();
        if (offer == null)
        {
            return result;
        }
        foreach (RecombAffix a in offer.All)
        {
            if (selected.Contains(Key(a)))
            {
                result.Add(a);
            }
        }
        return result;
    }

    private static void ClickSlot(int index)
    {
        if (pouring)
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
            if (slots[index] != null)
            {
                GiveBack(slots[index]);
                slots[index] = null;
            }
            else
            {
                var content = manager.cursor.GetContent();
                if (content != null && content.Count > 0)
                {
                    ItemContainerEntry entry = content[0];
                    ItemData item = entry.data;
                    if (!item.IsNullOrDestroyed() && manager.cursor.TryRemoveItem(entry, 1, Context.DEFAULT))
                    {
                        slots[index] = item;
                    }
                }
                else
                {
                    message = "Pick up an item first, then click the slot.";
                }
            }
            selected.Clear();
            dirty = true;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Recombinator slot failed: " + ex.Message);
        }
    }

    private static void GiveBack(ItemData item)
    {
        ItemContainersManager manager = ItemContainersManager.Instance;
        if (item.IsNullOrDestroyed() || manager.IsNullOrDestroyed())
        {
            return;
        }
        if (!manager.inventory.TryAddItem(item, 1, Context.DEFAULT))
        {
            manager.cursor.TryAddItem(item, 1, Context.DEFAULT);
        }
    }

    private static void ReturnItems()
    {
        for (int i = 0; i < 2; i++)
        {
            if (slots[i] != null)
            {
                GiveBack(slots[i]);
                slots[i] = null;
            }
        }
        if (resultItem != null)
        {
            GiveBack(resultItem);
            resultItem = null;
            resultRevealed = false;
        }
        selected.Clear();
    }

    private static void DoRecombine(long gold, int embers, int bones)
    {
        try
        {
            if (offer == null || pouring)
            {
                return;
            }
            if (HaveGold() < gold)
            {
                Notify("Not enough gold.");
                return;
            }
            if (HaveEmbers() < embers)
            {
                try
                {
                    Notifications.NotEnoughSoulEmbers();
                }
                catch (Exception)
                {
                    Notify("Not enough Soul Embers.");
                }
                return;
            }
            if (bones > 0 && HaveBones() < bones)
            {
                Notify("Not enough Ancient Bones.");
                return;
            }
            if (resultItem != null)
            {
                message = "Take the result out of the bottom slot first.";
                dirty = true;
                return;
            }
            List<RecombAffix> picks = Picks();
            if (deterministic && (picks.Count == 0 || RecombinatorMath.Validate(picks, Math.Max(offer.CapacityA, offer.CapacityB)).Length > 0))
            {
                Notify(picks.Count == 0 ? "Pick the affixes you want to keep." : RecombinatorMath.Validate(picks, Math.Max(offer.CapacityA, offer.CapacityB)));
                return;
            }
            ItemData result = Items_Recombinator.Recombine(offer, deterministic, picks, out bool exact, out string note, bones > 0);
            if (result.IsNullOrDestroyed())
            {
                message = note;
                dirty = true;
                return;
            }
            // Pay only now that there is a result.
            Refs_Manager.player_gold_tracker?.modifyGold((int)-Math.Min(gold, int.MaxValue));
            DungeonRunManager runs = Character_SoulEmbers.GetManager();
            runs?.modifySoulEmberCountOnSingleplayerOrServer(-embers, true);
            if (bones > 0)
            {
                BonesTracker()?.modifyAncientBones(-bones);
            }

            pourItems[0] = slots[0];
            pourItems[1] = slots[1];
            slots[0] = null;
            slots[1] = null;
            resultItem = result;
            icons[2]?.Dispose();
            icons[2] = BuildIcon(result); // started now so its sprite has loaded by the time it appears
            iconFor[2] = (long)result.Pointer;
            StartPour(bones > 0);
            selected.Clear();
            message = "";
            lastFailed = deterministic && !exact;
            dirty = true;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Recombination failed: " + ex.Message);
            message = "Something went wrong; nothing was spent.";
            dirty = true;
        }
    }

    internal static AncientBonesTracker BonesTracker()
    {
        AncientBonesTracker tracker = PlayerFinder.getAncientBonesTracker();
        if (tracker.IsNullOrDestroyed() && !Refs_Manager.player_actor.IsNullOrDestroyed())
        {
            tracker = Refs_Manager.player_actor.ancientBonesTracker;
        }
        return tracker.IsNullOrDestroyed() ? null : tracker;
    }

    internal static long HaveBones()
    {
        try
        {
            AncientBonesTracker tracker = BonesTracker();
            if (tracker == null)
            {
                return 0;
            }
            long lo = 0, hi = 2_000_000_000;
            while (lo < hi)
            {
                long mid = (lo + hi + 1) / 2;
                if (tracker.CanAfford((int)mid))
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return lo;
        }
        catch (Exception)
        {
            return 0;
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

    internal static long HaveGold()
    {
        try
        {
            ItemContainersManager manager = ItemContainersManager.Instance;
            GoldTracker tracker = Refs_Manager.player_gold_tracker;
            if (tracker.IsNullOrDestroyed())
            {
                return 0;
            }
            // CanAfford probes without spending: find the largest amount it accepts.
            long lo = 0, hi = 2_000_000_000;
            while (lo < hi)
            {
                long mid = (lo + hi + 1) / 2;
                if (tracker.CanAfford((int)mid))
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return lo;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static int HaveEmbers()
    {
        try
        {
            DungeonRunManager runs = Character_SoulEmbers.GetManager();
            return runs.IsNullOrDestroyed() ? 0 : runs.GetSoulEmbers();
        }
        catch (Exception)
        {
            return 0;
        }
    }

    // ---------------------------------------------------------------- text helpers

    private static string ItemTitle(ItemData item)
    {
        string name = item.TryCast<ItemDataUnpacked>()?.FullName;
        return string.IsNullOrEmpty(name) ? "Item" : name;
    }

    private static string ItemSubtitle(ItemData item)
    {
        int ordinary = item.affixes.ToArray().Count(x => x != null && !x.IsSealed);
        return ordinary + " affix" + (ordinary == 1 ? "" : "es") + (item.affixes.ToArray().Any(x => x != null && x.IsSealed) ? ", sealed" : "");
    }

    private static readonly Regex HexColour = new("#?([0-9A-Fa-f]{6})", RegexOptions.Compiled);

    /// <summary>The item's rarity colour, taken from the game's own name colouring.</summary>
    internal static Color ItemColor(ItemData item)
    {
        try
        {
            ItemDataUnpacked data = item.TryCast<ItemDataUnpacked>();
            if (data != null)
            {
                Match m = HexColour.Match(TooltipItemManager.SetNameColor(data) ?? "");
                if (m.Success)
                {
                    int v = Convert.ToInt32(m.Groups[1].Value, 16);
                    return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
                }
            }
        }
        catch (Exception)
        {
        }
        return ItemColorFallback(item);
    }

    private static Color ItemColorFallback(ItemData item)
    {
        bool exalted = item.affixes.ToArray().Any(x => x != null && x.affixTier >= 5);
        if (exalted)
        {
            return new Color(0.82f, 0.55f, 1f);
        }
        return item.rarity <= 1 ? new Color(0.5f, 0.65f, 1f) : Gold;
    }

    private static string AffixText(RecombAffix a)
    {
        AffixList.Affix def = AffixList.get()?.GetAffix(a.AffixId);
        string name = def == null ? "Affix " + a.AffixId : NativeItemNames.AffixName(def);
        return name + "   T" + a.DisplayedTier;
    }
}
