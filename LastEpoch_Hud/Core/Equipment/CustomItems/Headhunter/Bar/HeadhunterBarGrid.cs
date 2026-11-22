using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;

/// <summary>Icon cells of the bar in canvas units from its bottom-center: rows of PerRow, bottom row first, each row centered.</summary>
public readonly record struct HeadhunterBarGrid(int Count, int PerRow)
{
    private const float Step = HeadhunterBarLayout.EntrySize + HeadhunterBarLayout.Spacing;

    public int Rows
    {
        get
        {
            if (Count <= 0 || PerRow <= 0)
            {
                return 0;
            }

            return ((Count - 1) / PerRow) + 1;
        }
    }

    public float Width
    {
        get
        {
            if (Rows == 0)
            {
                return 0f;
            }

            return HeadhunterBarLayout.PanelWidth(Math.Min(Count, PerRow));
        }
    }

    public float Height
    {
        get
        {
            if (Rows == 0)
            {
                return 0f;
            }

            return (Rows * HeadhunterBarLayout.EntrySize)
                + ((Rows - 1) * HeadhunterBarLayout.Spacing);
        }
    }

    public int CountInRow(int row)
    {
        if (row < 0 || row >= Rows)
        {
            return 0;
        }

        return Math.Min(PerRow, Count - (row * PerRow));
    }

    public float CellX(int index)
    {
        if (PerRow <= 0)
        {
            return 0f;
        }

        int inRow = CountInRow(index / PerRow);
        return ((index % PerRow) - ((inRow - 1) / 2f)) * Step;
    }

    public float CellBottom(int index)
    {
        if (PerRow <= 0)
        {
            return 0f;
        }

        return (index / PerRow) * Step;
    }

    public int IndexAt(float localX, float localY)
    {
        if (Rows == 0 || localY < 0f)
        {
            return -1;
        }

        int row = (int)(localY / Step);
        if (row >= Rows || localY - (row * Step) >= HeadhunterBarLayout.EntrySize)
        {
            return -1;
        }

        int inRow = CountInRow(row);
        float offset = localX + (HeadhunterBarLayout.PanelWidth(inRow) / 2f);
        if (offset < 0f)
        {
            return -1;
        }

        int col = (int)(offset / Step);
        if (col >= inRow || offset - (col * Step) >= HeadhunterBarLayout.EntrySize)
        {
            return -1;
        }

        return (row * PerRow) + col;
    }
}
