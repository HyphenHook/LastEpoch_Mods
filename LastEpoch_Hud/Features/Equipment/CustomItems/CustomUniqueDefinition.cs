using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Affixes;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

/// <summary>Everything one custom unique hands to the registrar.</summary>
public sealed class CustomUniqueDefinition
{
    public CustomUniqueSpec Spec { get; init; }

    /// <summary>Null when the item uses a base the game already has.</summary>
    public string SubtypeNameKey { get; init; }
    public string UniqueNameKey { get; init; }
    public string LoreKey { get; init; }

    /// <summary>Mod texts to description; null when the item has no description.</summary>
    public Func<IReadOnlyDictionary<string, string>, string> Description { get; init; }
    public Func<CustomUniqueFlags> Flags { get; init; }

    /// <summary>Null when the item uses a base the game already has.</summary>
    public IReadOnlyList<CustomBaseImplicit> Implicits { get; init; }
    public IReadOnlyList<CustomUniqueMod> Mods { get; init; }

    /// <summary>Read at registration, so a save setting can pick the entries.</summary>
    public Func<IReadOnlyList<byte>> TooltipEntries { get; init; }
}
