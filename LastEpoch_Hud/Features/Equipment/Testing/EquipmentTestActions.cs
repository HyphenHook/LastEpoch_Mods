using System.Collections.Generic;
using LastEpoch_Hud.Core.Testing;
using LastEpoch_Hud.Scripts.Mods.Items.Charms;
using LastEpoch_Hud.Scripts.Mods.Items.Jewels;

namespace LastEpoch_Hud.Features.Equipment.Testing;

internal sealed class EquipmentTestActions : ITestActionProvider
{
    public IEnumerable<TestAction> GetActions()
    {
        yield return new TestAction("GiveRareJewels", "Give four Rare Jewel subtypes", () => JewelItems.GiveSet(false));
        yield return new TestAction("GiveExaltedJewels", "Give four Exalted Jewel subtypes (2 LP)", () => JewelItems.GiveSet(true));
        yield return new TestAction("GiveTestCharm", "Give Intelligence Test Charm", CharmEquipment.GiveTestCharm);
        yield return new TestAction("DropTestCharm", "Drop Intelligence Test Charm at my feet", CharmEquipment.DropTestCharm);
    }
}
