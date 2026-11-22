namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;

/// <summary>Converts screen pixels to bar grid units and back.</summary>
public static class HeadhunterBarGeometry
{
    public static int IndexAt(
        HeadhunterBarPlacement placement,
        float canvasScale,
        HeadhunterBarGrid grid,
        float x,
        float y
    )
    {
        if (placement.Scale <= 0f || canvasScale <= 0f)
        {
            return -1;
        }

        float scale = placement.Scale * canvasScale;
        return grid.IndexAt((x - placement.X) / scale, (y - placement.Y) / scale);
    }

    public static (float X, float Y) TooltipAnchor(
        HeadhunterBarPlacement placement,
        float canvasScale,
        HeadhunterBarGrid grid,
        int index
    )
    {
        float scale = placement.Scale * canvasScale;
        return (placement.X + (grid.CellX(index) * scale), placement.Y + (grid.Height * scale));
    }
}
