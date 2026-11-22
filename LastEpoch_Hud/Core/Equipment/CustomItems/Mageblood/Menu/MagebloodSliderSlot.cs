namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;

/// <summary>One editable field of a slot's flask: the file row it sits in and its target.</summary>
public readonly record struct MagebloodSliderSlot(int RowIndex, MagebloodValueTarget Target);
