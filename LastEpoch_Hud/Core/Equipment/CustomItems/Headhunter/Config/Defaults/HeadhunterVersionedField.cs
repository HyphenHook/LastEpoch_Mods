using Newtonsoft.Json.Linq;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Defaults;

/// <summary>A default field plus the defaults version that introduced it. Parent is "" for the root, else a root object key.</summary>
public readonly record struct HeadhunterVersionedField(
    string Parent,
    string Key,
    JToken Value,
    int Since
);
