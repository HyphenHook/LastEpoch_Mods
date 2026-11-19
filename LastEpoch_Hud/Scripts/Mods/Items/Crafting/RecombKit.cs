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

    public const float SL = 0.823f, SR = 0.807f; // the two input frames, each shrunk so its opening is exactly InW wide
    public const float P = 0.7f; // the pipes
    public const float RS = 0.672f; // the result frame, shrunk so its opening is as wide as the other two
    public const float CenterX = 383f;
    public const float InCx0 = CenterX - 135f, InCx1 = CenterX + 135f; // the middle of the two item boxes: the same distance from the centre
    public const float InY = 135f, ItemH = 196f; // the top of the item boxes, and their height
    public const float InW = 121f, ResW = 121f, ResH = 186f; // what the frames leave for an item: the two inputs, and the result

    public static Rect LeftFrame => new(InCx0 - 202f * SL, InY - 117f * SL, 338f * SL, 396f * SL);
    public static Rect RightFrame => new(InCx1 - 144.5f * SR, InY - 117f * SR, 355f * SR, 396f * SR);

    // the corner pipes tuck under the bottom of the input frames, and the cross pipe under the top of the result frame
    private static float PipeTop => LeftFrame.yMax - 36f; // far enough up that the bottom diamond of each frame covers the pipe
    public static float PipeLevel => PipeTop + 116.5f * P; // the middle of the pipes that run across
    public static Rect LeftCorner => new(InCx0 - 42.5f * P, PipeTop, 193f * P, 168f * P);
    public static Rect RightCorner => new(InCx1 - 144.5f * P, PipeTop, 192f * P, 169f * P);
    public static Rect Cross => new(CenterX - 92.5f * P, PipeLevel - 96f * P, 185f * P, 211f * P);
    public static Rect ResultFrame => new(CenterX - 275.5f * RS, Cross.y + 203f * P - 42f, 553f * RS, 413f * RS);
    public static float ResY => ResultFrame.y + 92f * RS; // the top of the result's item box
    public static float AffixTop => ResY; // the affix lists start level with the result's item box, one each side of it
    public static float Bottom => ResultFrame.yMax;

    public static Rect Frame(int id) => id == 0 ? LeftFrame : id == 1 ? RightFrame : ResultFrame;
    public static string FrameComponent(int id) => id == 0 ? "ItemSlot_Left" : id == 1 ? "ItemSlot_Right" : "ItemSlot_Result";

    // ---------------------------------------------------------------- the timeline, in seconds

    public const float DevourLeftAt = 0f, DevourRightAt = 0.1f; // the flame climbs the slot and swallows the item
    public const float CornerFlowAt = 0.95f, CrossFlowAt = 1.2f;
    public const float PourAt = 1.95f;
    public const float RevealAt = 2.5f; // the item appears, with the flash
    public const float FadeAt = 2.55f, FadeSeconds = 0.4f;
    public const float EndAt = FadeAt + FadeSeconds + 0.05f;

    /// <summary>How much of an input item is left: it is eaten while the flame stands over it.</summary>
    public static float InputAlpha(int side, float now)
    {
        if (!playing)
        {
            return 1f;
        }
        float t = now - startedAt - (side == 0 ? DevourLeftAt : DevourRightAt);
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.37f) / 0.41f));
    }

    private sealed class Seq
    {
        public Image Img;
        public Sprite[] Frames;
        public string Folder;
        public float Start, Fps, EndHide = float.MaxValue; // EndHide: the time it is taken away
        public bool Loop, Hold, Fades; // Hold: the last frame stays; Fades: it fades with the pipes
        public int Shown = -1;
    }

    private static readonly Image[] pipeBases = new Image[3];
    private static GameObject fxRoot, pipeRoot; // the pipes go under the frames; the effects over them
    private static readonly List<Seq> seqs = new();
    private static float startedAt;
    private static bool playing;

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
        "Pipe_LeftCorner/VFX_Flow_Orange", "Pipe_RightCorner/VFX_Flow_Green",
        "Pipe_MiddleCenter/VFX_Flow_Orange", "Pipe_MiddleCenter/VFX_Flow_Green",
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

    private static GameObject Root(Transform content, string name)
    {
        var go = new GameObject(name);
        RectTransform r = go.AddComponent<RectTransform>();
        go.transform.SetParent(content, false);
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return go;
    }

    /// <summary>Puts the pipes down before the window puts its frames up, so the frames lie over the pipes.</summary>
    public static void LowerPipes()
    {
        if (pipeRoot != null)
        {
            pipeRoot.transform.SetAsLastSibling();
        }
    }

    /// <summary>Makes the pipes and the effect layers if they are not there. They are made once and live as long as the window.</summary>
    public static void Ensure(Transform content)
    {
        if (fxRoot != null)
        {
            return;
        }
        pipeRoot = Root(content, "RecombinatorPipes");
        fxRoot = Root(content, "RecombinatorFx");
        seqs.Clear();
        Transform pipes = pipeRoot.transform;
        Transform t = fxRoot.transform;

        // the pipes: plain grey until something flows in them
        pipeBases[0] = Layer(pipes, "PipeLeft", LeftCorner, RecombArt.Base("Pipe_LeftCorner"));
        pipeBases[1] = Layer(pipes, "PipeRight", RightCorner, RecombArt.Base("Pipe_RightCorner"));
        pipeBases[2] = Layer(pipes, "PipeCross", Cross, RecombArt.Base("Pipe_MiddleCenter"));
        Add(pipes, "FlowLeft", LeftCorner, "Pipe_LeftCorner/VFX_Flow_Orange", CornerFlowAt, 24f, true, false, true);
        Add(pipes, "FlowRight", RightCorner, "Pipe_RightCorner/VFX_Flow_Green", CornerFlowAt, 24f, true, false, true);
        Add(pipes, "FlowCrossOrange", Cross, "Pipe_MiddleCenter/VFX_Flow_Orange", CrossFlowAt, 24f, true, false, true);
        Add(pipes, "FlowCrossGreen", Cross, "Pipe_MiddleCenter/VFX_Flow_Green", CrossFlowAt, 24f, true, false, true);

        // the flames over the input slots
    }

    public static void ApplyPipeSkin(bool primordial)
    {
        SetPipeSprite(pipeBases[0], "primordial_pipe_left", "Pipe_LeftCorner", primordial);
        SetPipeSprite(pipeBases[1], "primordial_pipe_right", "Pipe_RightCorner", primordial);
        SetPipeSprite(pipeBases[2], "primordial_pipe_center", "Pipe_MiddleCenter", primordial);
    }

    private static void SetPipeSprite(Image image, string fossil, string standard, bool primordial)
    {
        Sprite skin = primordial ? OrbArt.Named(fossil) : null;
        image.sprite = skin != null ? skin : RecombArt.Base(standard);
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

    /// <summary>Starts the input and pipe effects.</summary>
    public static void Start(float now)
    {
        startedAt = now;
        playing = true;
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
            if (into < 0 || t >= s.EndHide)
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
