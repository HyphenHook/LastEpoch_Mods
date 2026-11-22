using System;
using System.Collections.Generic;
using LastEpoch_Hud.Core.Testing;

namespace LastEpoch_Hud.Application.Testing;

/// <summary>Materializes feature commands once and rejects ambiguous UI command IDs.</summary>
public sealed class TestActionCatalog
{
    public IReadOnlyList<TestAction> Actions { get; }

    public TestActionCatalog(params ITestActionProvider[] providers)
    {
        if (providers == null) throw new ArgumentNullException(nameof(providers));
        var actions = new List<TestAction>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            if (provider == null) throw new ArgumentException("A command provider cannot be null.", nameof(providers));
            foreach (var action in provider.GetActions())
            {
                if (action == null || !ids.Add(action.Id))
                    throw new ArgumentException("Test commands must be non-null and have unique IDs.", nameof(providers));
                actions.Add(action);
            }
        }
        Actions = actions.AsReadOnly();
    }
}
