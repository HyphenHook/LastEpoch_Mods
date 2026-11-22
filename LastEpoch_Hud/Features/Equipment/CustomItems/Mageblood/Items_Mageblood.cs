using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Affixes;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood.Bar;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood.Menu;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood;

[RegisterTypeInIl2Cpp]
public class Items_Mageblood : MonoBehaviour
{
    private static readonly CustomUniqueRegistrar _registrar = new(CreateDefinition());

    public Items_Mageblood(System.IntPtr ptr)
        : base(ptr) { }

    private void Awake()
    {
        MagebloodConfigLoader.Load();
        SceneManager.add_sceneLoaded(new System.Action<Scene, LoadSceneMode>(OnSceneLoaded));
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        MagebloodBuffs.OnSceneLoaded(scene.name);
    }

    private void Update()
    {
        _registrar.Update();
        MagebloodMenuWriter.Tick(Time.unscaledTime);
        if (MagebloodConfigLoader.ReloadIfChanged(Time.unscaledTime))
        {
            MagebloodBuffs.MarkDirty();
        }

        MagebloodBuffs.Tick(Time.unscaledTime);
        MagebloodBarHover.Tick();
    }

    private static CustomUniqueDefinition CreateDefinition()
    {
        return new CustomUniqueDefinition
        {
            Spec = CustomUniqueSpecs.Mageblood,
            SubtypeNameKey = CustomItemLocaleKeys.MagebloodSubtype,
            UniqueNameKey = CustomItemLocaleKeys.MagebloodName,
            LoreKey = CustomItemLocaleKeys.MagebloodLore,
            Description = texts =>
                MagebloodDescription.Text(texts, MagebloodConfigLoader.Current.MaxResistances),
            Flags = () => CustomUniqueFlags.NoSettings,
            Implicits = CustomUniqueAffixes.MagebloodImplicits,
            Mods = CustomUniqueAffixes.MagebloodMods,
            TooltipEntries = () => CustomUniqueAffixes.MagebloodTooltip,
        };
    }
}
