using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;

/// <summary>Screen rect of the skill bar in pixels.</summary>
public readonly record struct SkillBarBounds(float MinX, float MinY, float MaxX, float MaxY)
{
    public static SkillBarBounds Empty { get; } =
        new(
            float.PositiveInfinity,
            float.PositiveInfinity,
            float.NegativeInfinity,
            float.NegativeInfinity
        );

    public bool IsEmpty => MinX > MaxX || MinY > MaxY;

    public float Height => MaxY - MinY;

    public float CenterX => (MinX + MaxX) * 0.5f;

    public static SkillBarBounds ScreenFallback(float screenWidth, float screenHeight)
    {
        return new SkillBarBounds(
            screenWidth * 0.4f,
            screenHeight * 0.06f,
            screenWidth * 0.6f,
            screenHeight * 0.12f
        );
    }

    public SkillBarBounds Include(float x, float y)
    {
        return new SkillBarBounds(
            Math.Min(MinX, x),
            Math.Min(MinY, y),
            Math.Max(MaxX, x),
            Math.Max(MaxY, y)
        );
    }

    /// <summary>Includes the rect with the icon's own scale divided out (AutoCast pulse).</summary>
    public SkillBarBounds IncludeRect(
        float minX,
        float minY,
        float maxX,
        float maxY,
        float iconScale
    )
    {
        float factor = iconScale > 0f ? 1f / iconScale : 1f;
        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;
        float halfWidth = (maxX - minX) * 0.5f * factor;
        float halfHeight = (maxY - minY) * 0.5f * factor;
        return Include(centerX - halfWidth, centerY - halfHeight)
            .Include(centerX + halfWidth, centerY + halfHeight);
    }
}
