using LastEpoch_Hud.Application.Testing;
using LastEpoch_Hud.Features.Crafting.Testing;
using LastEpoch_Hud.Features.Equipment.Testing;

namespace LastEpoch_Hud.Bootstrap.Composition;

internal static class TestActionFactory
{
    internal static TestActionCatalog Create() => new(new EquipmentTestActions(), new OrbTestActions());
}
