using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Affixes;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems;
using MelonLoader;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.SandsOfSilk;

[RegisterTypeInIl2Cpp]
public class Items_SandsOfSilk : MonoBehaviour
{
    private static readonly CustomUniqueRegistrar _registrar = new(CreateDefinition());

    public Items_SandsOfSilk(System.IntPtr ptr)
        : base(ptr) { }

    private void Update()
    {
        _registrar.Update();
    }

    private static CustomUniqueDefinition CreateDefinition()
    {
        return new CustomUniqueDefinition
        {
            Spec = CustomUniqueSpecs.SandsOfSilk,
            SubtypeNameKey = CustomItemLocaleKeys.SandsOfSilkSubtype,
            UniqueNameKey = CustomItemLocaleKeys.SandsOfSilkName,
            LoreKey = CustomItemLocaleKeys.SandsOfSilkLore,
            Flags = () => CustomUniqueFlags.NoSettings,
            Implicits = CustomUniqueAffixes.SandsOfSilkImplicits,
            Mods = CustomUniqueAffixes.SandsOfSilkMods,
            TooltipEntries = () => CustomUniqueAffixes.SandsOfSilkTooltip,
        };
    }
}
