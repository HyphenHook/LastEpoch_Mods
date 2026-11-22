using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Json;

/// <summary>A usable config plus every problem found while reading it.</summary>
public sealed class HeadhunterConfigParseResult
{
    public HeadhunterConfig Config { get; init; }
    public IReadOnlyList<HeadhunterConfigProblem> Problems { get; init; }
    public bool IsReadable { get; init; }
}
