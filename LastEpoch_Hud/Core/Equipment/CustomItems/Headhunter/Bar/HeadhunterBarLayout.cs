using System;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;

/// <summary>Places the buff bar above the skill bar and decides when it must move.</summary>
public static class HeadhunterBarLayout
{
    public const float GapRatio = 0.2f;
    public const float SizeRatio = 0.75f;
    public const float MoveTolerance = 0.02f;
    public const float EntrySize = 60f;
    public const float Spacing = 4f;

    public static bool TryPlace(
        SkillBarBounds bounds,
        HeadhunterBarSettings settings,
        float entryHeight,
        float canvasScale,
        out HeadhunterBarPlacement placement
    )
    {
        placement = default;
        if (bounds.IsEmpty || bounds.Height <= 0f || entryHeight <= 0f || canvasScale <= 0f)
        {
            return false;
        }
        if (settings.IconSize <= 0f)
        {
            return false;
        }

        float baseScale = (bounds.Height * SizeRatio) / entryHeight / canvasScale;
        float rowPixels = (EntrySize + Spacing) * baseScale * canvasScale;
        placement = new HeadhunterBarPlacement(
            bounds.CenterX + (settings.OffsetX * rowPixels),
            bounds.MaxY + (bounds.Height * GapRatio) + (settings.OffsetY * rowPixels),
            baseScale * settings.IconSize
        );
        return true;
    }

    public static bool ShouldMove(
        HeadhunterBarPlacement current,
        HeadhunterBarPlacement next,
        float entryHeight
    )
    {
        if (next.ScreenW != current.ScreenW || next.ScreenH != current.ScreenH)
        {
            return true;
        }

        float shiftLimit = MoveTolerance * next.Scale * entryHeight;
        if (Math.Abs(next.X - current.X) > shiftLimit)
        {
            return true;
        }

        if (Math.Abs(next.Y - current.Y) > shiftLimit)
        {
            return true;
        }

        return Math.Abs(next.Scale - current.Scale) > MoveTolerance * current.Scale;
    }

    /// <summary>Panel position in canvas units, relative to the screen center (anchors 0.5/0.5).</summary>
    public static (float X, float Y) ToLocal(
        HeadhunterBarPlacement placement,
        float screenW,
        float screenH,
        float canvasScale
    )
    {
        return ToLocal(placement.X, placement.Y, screenW, screenH, canvasScale);
    }

    /// <summary>Screen pixels to canvas units, relative to the screen center.</summary>
    public static (float X, float Y) ToLocal(
        float x,
        float y,
        float screenW,
        float screenH,
        float canvasScale
    )
    {
        return ((x - (screenW * 0.5f)) / canvasScale, (y - (screenH * 0.5f)) / canvasScale);
    }

    /// <summary>Row width in canvas units for the given icon count.</summary>
    public static float PanelWidth(int count)
    {
        if (count <= 0)
        {
            return 0f;
        }

        return (count * EntrySize) + ((count - 1) * Spacing);
    }
}
