namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

/// <summary>The merged file text, whether it differs from the input, and how many flasks were appended.</summary>
public readonly record struct MagebloodMergeResult(string Text, bool Changed, int Added);
