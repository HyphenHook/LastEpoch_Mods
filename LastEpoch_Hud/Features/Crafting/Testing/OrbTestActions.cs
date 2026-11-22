using LastEpoch_Hud.Scripts;
using System.Collections.Generic;
using LastEpoch_Hud.Core.Testing;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using LastEpoch_Hud.Scripts.Mods.Items.Drop;

namespace LastEpoch_Hud.Features.Crafting.Testing;

internal sealed class OrbTestActions : ITestActionProvider
{
    public IEnumerable<TestAction> GetActions()
    {
        foreach (var material in AdvancedMaterials.All)
        {
            string key = material.Key;
            yield return new TestAction("Give" + key, "Give 5 " + material.Name + " (testing)",
                () => AdvancedMaterials.Add(key, 5));
            yield return new TestAction("Drop" + key, "Drop an " + material.Name + " at my feet (testing)", () =>
            {
                if (!Refs_Manager.player_actor.IsNullOrDestroyed()) OrbDrop.Drop(Refs_Manager.player_actor.position(), key);
            });
        }
    }
}
