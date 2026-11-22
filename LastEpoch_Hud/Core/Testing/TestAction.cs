using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Core.Testing;

/// <summary>A developer command supplied by its owning feature, independent of its UI.</summary>
public sealed class TestAction
{
    public string Id { get; }
    public string Label { get; }
    public Action Execute { get; }

    public TestAction(string id, string label, Action execute)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An action needs an ID.", nameof(id));
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("An action needs a label.", nameof(label));
        Id = id;
        Label = label;
        Execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }
}

public interface ITestActionProvider
{
    IEnumerable<TestAction> GetActions();
}
