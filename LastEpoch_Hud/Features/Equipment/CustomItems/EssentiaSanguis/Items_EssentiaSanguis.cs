using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Affixes;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems;
using MelonLoader;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.EssentiaSanguis;

[RegisterTypeInIl2Cpp]
public class Items_EssentiaSanguis : MonoBehaviour
{
    private static readonly CustomUniqueRegistrar _registrar = new(CreateDefinition());

    public Items_EssentiaSanguis(System.IntPtr ptr)
        : base(ptr) { }

    private void Update()
    {
        _registrar.Update();
    }

    private static CustomUniqueDefinition CreateDefinition()
    {
        return new CustomUniqueDefinition
        {
            Spec = CustomUniqueSpecs.EssentiaSanguis,
            SubtypeNameKey = CustomItemLocaleKeys.EssentiaSanguisSubtype,
            UniqueNameKey = CustomItemLocaleKeys.EssentiaSanguisName,
            LoreKey = CustomItemLocaleKeys.EssentiaSanguisLore,
            Description = texts =>
                LocaleText.Get(texts, CustomItemLocaleKeys.EssentiaSanguisDescription),
            Flags = () => CustomUniqueFlags.NoSettings,
            Implicits = CustomUniqueAffixes.EssentiaSanguisImplicits,
            Mods = CustomUniqueAffixes.EssentiaSanguisMods,
            TooltipEntries = () => CustomUniqueAffixes.EssentiaSanguisTooltip,
        };
    }
}
