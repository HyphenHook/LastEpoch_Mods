namespace LastEpoch_Hud.Scripts.Core.Items.AutoLoot;

/// <summary>Actions for a filter-path item, tried in order: PickUp, else Shatter, else Sell.</summary>
public readonly record struct FilterLootPlan(bool PickUp, bool Shatter, bool Sell)
{
    public bool IsEmpty => !PickUp && !Shatter && !Sell;
}
