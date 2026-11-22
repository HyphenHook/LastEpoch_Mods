using System;

namespace LastEpoch_Hud.Scripts.Core.Items.AutoLoot;

/// <summary>Marks drops made by the mod while they run.</summary>
public sealed class DropOriginTracker
{
    private int _depth;

    public DropOrigin Current => _depth > 0 ? DropOrigin.Mod : DropOrigin.Game;

    public void RunAsMod(Action drop)
    {
        _depth++;
        try
        {
            drop();
        }
        finally
        {
            _depth--;
        }
    }
}
