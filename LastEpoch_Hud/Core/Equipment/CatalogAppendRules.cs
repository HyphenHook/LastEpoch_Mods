using System;

namespace LastEpoch_Hud.Core.Equipment;

/// <summary>Saved base IDs are permanent; registration must never replace or skip a category.</summary>
public static class CatalogAppendRules
{
    public static void Validate(int id, int currentCount, int nativeMaximum, bool occupiedByNonEquipment)
    {
        if (id < 0 || id > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(id));
        if (id <= nativeMaximum) throw new InvalidOperationException("A custom base ID overlaps the native equipment enum.");
        if (id != currentCount) throw new InvalidOperationException("Custom equipment registration must append in saved-ID order.");
        if (occupiedByNonEquipment) throw new InvalidOperationException("The base ID belongs to a non-equipment category.");
    }
}
