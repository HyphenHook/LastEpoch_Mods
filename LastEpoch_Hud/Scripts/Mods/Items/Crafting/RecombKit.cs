using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// The Recombinator's frames and pipes, and the effect that runs over them when two items are recombined. All the
/// positions are in one place. The pipes are grey; nothing in them flows until the effect starts.
///
/// The sequence: the left slot powers up in orange, then the right in green; orange and green flow down their own pipes;
/// the essence pools in the result slot and pours down into the item; the item appears with an orange and a green flash;
/// the pipes fade back to grey.
/// </summary>
internal static class RecombKit
{
    // ---------------------------------------------------------------- where everything goes (window coordinates, y down)

    public const float S = 0.817f; // the frames, shrunk by this from the supplied size
    public const float P = 0.7f; // the pipes
    public const float CenterX = 383f;
    public const float InCx0 = 248f, InCx1 = 518f; // the middle of the two item boxes
    public const float InY = 135f, ItemH = 196f; // the top of the item boxes, and their height
    public const float InW = 121f, ResW = 148f; // what the frames leave for an item: the two inputs, and the result

    public static Rect LeftFrame => new(InCx0 - 202f * S, InY - 117f * S, 338f * S, 396f * S);
    public static Rect RightFrame => new(InCx1 - 144.5f * S, InY - 117f * S, 355f * S, 396f * S);
    private static float PipeTop => LeftFrame.yMax - 6f;
    public static float PipeLevel => PipeTop + 115f * P; // the middle of the pipes that run across
    public static Rect LeftCorner => new(InCx0 - 60f * P, PipeTop, 193f * P, 168f * P);
    public static Rect RightCorner => new(InCx1 - 147f * P, PipeTop, 192f * P, 169f * P);
    public static Rect Cross => new(CenterX - 88f * P, PipeLevel - 97f * P, 185f * P, 211f * P);
    public static Rect ResultFrame => new(CenterX - 275.5f * S, Cross.yMax - 6f, 553f * S, 413f * S);
    public static float ResY => ResultFrame.y + 108f * S; // the top of the result's item box
    public static float Bottom => ResultFrame.yMax;

    public static Rect Frame(int id) => id == 0 ? LeftFrame : id == 1 ? RightFrame : ResultFrame;
    public static string FrameComponent(int id) => id == 0 ? "ItemSlot_Left" : id == 1 ? "ItemSlot_Right" : "ItemSlot_Result";

    // ---------------------------------------------------------------- the timeline, in seconds

    public const float LeftPowerAt = 0f, RightPowerAt = 1.0f;
    public const float FlowAt = 2.0f, CrossFlowAt = 2.35f;
    public const float PoolAt = 3.5f, PourAt = 4.3f;
    public const float RevealAt = 5.2f; // the item appears, with the flash
    public const float FadeAt = 5.7f, FadeSeconds = 0.65f;
    public const float EndAt = FadeAt + FadeSeconds + 0.05f;

    private sealed class Seq
    {
        public Image Img;
        public Sprite[] Frames;
        public string Folder;
        public float Start, Fps, EndHide = float.MaxValue; // EndHide: the time it is taken away
        public bool Loop, Hold, Fades; // Hold: the last frame stays; Fades: it fades with the pipes
        public int Shown = -1;
    }

    private static GameObject fxRoot;
    private static readonly List<Seq> seqs = new();
    private static float startedAt;
    private static bool playing, withFlash;

    public static bool Playing => playing;

    /// <summary>Every image the effect needs, so they can be read ahead of the first time it plays.</summary>
    public static IEnumerable<string> AllKeys()
    {
        foreach (string c in new[] { "ItemSlot_Left", "ItemSlot_Right", "ItemSlot_Result", "Pipe_LeftCorner", "Pipe_RightCorner", "Pipe_MiddleCenter" })
        {
            yield return c + "/Base/Dormant";
        }
        foreach (string folder in Folders())
        {
            foreach (string key in RecombArt.Keys(folder))
            {
                yield return key;
            }
        }
    }

    private static IEnumerable<string> Folders() => new[]
    {
        "ItemSlot_Left/VFX_PowerUp", "ItemSlot_Right/VFX_PowerUp",
        "Pipe_LeftCorner/VFX_Flow_Orange", "Pipe_RightCorner/VFX_Flow_Green",
        "Pipe_MiddleCenter/VFX_Flow_Orange", "Pipe_MiddleCenter/VFX_Flow_Green",
        "ItemSlot_Result/VFX_Pool_Orange", "ItemSlot_Result/VFX_Pool_Green",
        "ItemSlot_Result/VFX_Pour_Orange", "ItemSlot_Result/VFX_Pour_Green",
        "ItemSlot_Result/VFX_Flash_Orange", "ItemSlot_Result/VFX_Flash_Green",
    };

    private static bool preloaded;

    /// <summary>Reads a few of the images each frame while the window is open, so the first recombination does not stall.</summary>
    public static void Preload()
    {
        if (!preloaded)
        {
            preloaded = RecombArt.Preload(AllKeys(), 10);
        }
    }

    // ---------------------------------------------------------------- the pieces that stay: the pipes and the effect layers

    private static Image Layer(Transform parent, string name, Rect rect, Sprite sprite)
    {
        Image img = R.MakeImage(parent, name, Color.white);
        img.sprite = sprite;
        img.raycastTarget = false;
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(rect.x, -rect.y);
        r.sizeDelta = new Vector2(rect.width, rect.height);
        return img;
    }

    private static Seq Add(Transform parent, string name, Rect rect, string folder, float start, float fps, bool loop, bool hold, bool fades)
    {
        var s = new Seq { Folder = folder, Frames = new Sprite[0], Start = start, Fps = fps, Loop = loop, Hold = hold, Fades = fades };
        s.Img = Layer(parent, name, rect, null);
        s.Img.enabled = false;
        seqs.Add(s);
        return s;
    }

    /// <summary>Makes the pipes and the effect layers if they are not there. They are made once and live as long as the window.</summary>
    public static void Ensure(Transform content)
    {
        if (fxRoot != null)
        {
            return;
        }
        fxRoot = new GameObject("RecombinatorFx");
        RectTransform fr = fxRoot.AddComponent<RectTransform>();
        fxRoot.transform.SetParent(content, false);
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.offsetMin = fr.offsetMax = Vector2.zero;
        seqs.Clear();
        Transform t = fxRoot.transform;

        // the pipes: plain grey until something flows in them
        Layer(t, "PipeLeft", LeftCorner, RecombArt.Base("Pipe_LeftCorner"));
        Layer(t, "PipeRight", RightCorner, RecombArt.Base("Pipe_RightCorner"));
        Layer(t, "PipeCross", Cross, RecombArt.Base("Pipe_MiddleCenter"));

        // the effect layers, each the size of the piece it sits on
        Add(t, "PowerLeft", LeftFrame, "ItemSlot_Left/VFX_PowerUp", LeftPowerAt, 16f, false, true, true);
        Add(t, "PowerRight", RightFrame, "ItemSlot_Right/VFX_PowerUp", RightPowerAt, 16f, false, true, true);
        Add(t, "FlowLeft", LeftCorner, "Pipe_LeftCorner/VFX_Flow_Orange", FlowAt, 16f, true, false, true);
        Add(t, "FlowRight", RightCorner, "Pipe_RightCorner/VFX_Flow_Green", FlowAt, 16f, true, false, true);
        Add(t, "FlowCrossOrange", Cross, "Pipe_MiddleCenter/VFX_Flow_Orange", CrossFlowAt, 16f, true, false, true);
        Add(t, "FlowCrossGreen", Cross, "Pipe_MiddleCenter/VFX_Flow_Green", CrossFlowAt, 16f, true, false, true);
        foreach (string colour in new[] { "Orange", "Green" })
        {
            Add(t, "Pool" + colour, ResultFrame, "ItemSlot_Result/VFX_Pool_" + colour, PoolAt, 20f, false, true, false).EndHide = RevealAt;
            Add(t, "Pour" + colour, ResultFrame, "ItemSlot_Result/VFX_Pour_" + colour, PourAt, 18f, false, true, false).EndHide = RevealAt;
        }
        foreach (string colour in new[] { "Orange", "Green" })
        {
            Add(t, "Flash" + colour, ResultFrame, "ItemSlot_Result/VFX_Flash_" + colour, RevealAt, 16f, false, false, false);
        }
    }

    /// <summary>Puts the effect layers back on top, after the window has put its own pieces up again.</summary>
    public static void Raise()
    {
        if (fxRoot != null)
        {
            fxRoot.transform.SetAsLastSibling();
        }
    }

    // ---------------------------------------------------------------- playing

    /// <summary>Starts the effect. The flash is left out when the recombination failed.</summary>
    public static void Start(float now, bool flash)
    {
        startedAt = now;
        playing = true;
        withFlash = flash;
        foreach (Seq s in seqs)
        {
            s.Shown = -1;
            if (s.Frames.Length == 0)
            {
                s.Frames = RecombArt.Frames(s.Folder); // read already, as a rule, while the window was open
            }
        }
    }

    /// <summary>Takes the effect away at once.</summary>
    public static void Stop()
    {
        playing = false;
        foreach (Seq s in seqs)
        {
            if (s.Img != null)
            {
                s.Img.enabled = false;
            }
            s.Shown = -1;
        }
    }

    public static void Tick(float now)
    {
        if (!playing)
        {
            return;
        }
        float t = now - startedAt;
        if (t >= EndAt)
        {
            Stop();
            return;
        }
        float fade = t < FadeAt ? 1f : Mathf.Clamp01(1f - (t - FadeAt) / FadeSeconds);
        foreach (Seq s in seqs)
        {
            if (s.Img == null || s.Frames.Length == 0)
            {
                continue;
            }
            float into = t - s.Start;
            bool flash = s.Start == RevealAt;
            if (into < 0 || t >= s.EndHide || (flash && !withFlash))
            {
                s.Img.enabled = false;
                continue;
            }
            int frame = (int)(into * s.Fps);
            if (frame >= s.Frames.Length)
            {
                if (s.Loop)
                {
                    frame %= s.Frames.Length;
                }
                else if (s.Hold)
                {
                    frame = s.Frames.Length - 1;
                }
                else
                {
                    s.Img.enabled = false;
                    continue;
                }
            }
            if (frame != s.Shown)
            {
                s.Shown = frame;
                s.Img.sprite = s.Frames[frame];
            }
            s.Img.enabled = s.Img.sprite != null;
            s.Img.color = new Color(1f, 1f, 1f, s.Fades ? fade : 1f);
        }
    }
}
