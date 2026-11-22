using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Kills;

/// <summary>One stat of a monster mod, as plain data.</summary>
public sealed class MonsterModStat
{
    public MonsterModStat(
        string property,
        float added,
        float increased,
        IReadOnlyList<float> more,
        string tags
    )
    {
        Property = property;
        Added = added;
        Increased = increased;
        More = more;
        Tags = tags;
    }

    public string Property { get; }
    public float Added { get; }
    public float Increased { get; }
    public IReadOnlyList<float> More { get; }
    public string Tags { get; }
}
