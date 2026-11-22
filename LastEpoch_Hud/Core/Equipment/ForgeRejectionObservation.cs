using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.Items;

// One native capability check owns one observation. Labels from older checks or
// locales must never authorize a later rejection.
internal sealed class ForgeRejectionObservation
{
    private readonly string maxedKey;
    private readonly List<string> keys = new();
    private string maxedLabel;

    internal ForgeRejectionObservation(string maxedKey) => this.maxedKey = maxedKey;

    internal string ObservedKeys => string.Join(",", keys);

    internal void Observe(string key, string text)
    {
        if (string.IsNullOrEmpty(key))
            return;
        if (keys.Count < 8 && !keys.Contains(key))
            keys.Add(key);
        if (key == maxedKey)
            maxedLabel = text;
    }

    internal bool IsMaxedRejection(bool nativeAccepted, string title) =>
        !nativeAccepted
        && !string.IsNullOrWhiteSpace(maxedLabel)
        && string.Equals(title, maxedLabel, StringComparison.Ordinal);
}
