using HarmonyLib;
using Il2Cpp;
using Il2CppLE.AssetBundles;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.UI;

public partial class DamageMeter : MonoBehaviour
{

    static Il2CppSystem.Collections.Generic.List<Summoned> PlayerSummons()
    {
        if (Refs_Manager.player_actor.IsNullOrDestroyed())
        {
            return null;
        }
        SummonTracker tracker = null;
        if (
            !Refs_Manager.player_actor.TryGetExistingSummonTracker(out tracker)
            || tracker.IsNullOrDestroyed()
        )
        {
            return null;
        }
        return tracker.summons;
    }

    static Sprite AbilityIcon(Ability ability)
    {
        if (ability.IsNullOrDestroyed())
        {
            return null;
        }
        // The load ref is kept for the session so the sprite stays loaded while the meter shows it.
        if (!ability_icons.TryGetValue(ability.abilityName, out LoadRef<Sprite> loadRef))
        {
            SoftRef<Sprite> softRef = ability.abilitySpriteSoftRef;
            if (softRef == null || !softRef)
            {
                return null;
            }
            loadRef = SoftRefExtensions.CreateLoadRef(softRef, "LastEpoch_Hud", 0);
            if (loadRef == null)
            {
                return null;
            }
            loadRef.BlockForLoad();
            ability_icons[ability.abilityName] = loadRef;
        }
        return loadRef.AssetOrNull;
    }

    public class Assets
    {
        private static bool loading = false;

        public static bool Loaded()
        {
            bool result = false;
            if ((!DamageMeter_prefab.IsNullOrDestroyed()) && (!Skill_prefab.IsNullOrDestroyed()))
            {
                result = true;
            }

            return result;
        }

        public static void Load()
        {
            if ((!Hud_Manager.asset_bundle.IsNullOrDestroyed()) && (!loading))
            {
                loading = true;
                foreach (string name in Hud_Manager.asset_bundle.GetAllAssetNames())
                {
                    if (name.Contains("/damagemeter/"))
                    {
                        if (Functions.Check_Prefab(name) && name.Contains("damagemeter.prefab"))
                        {
                            DamageMeter_prefab = Hud_Manager
                                .asset_bundle.LoadAsset(name)
                                .TryCast<GameObject>();
                        }
                        else if (Functions.Check_Prefab(name) && name.Contains("skill.prefab"))
                        {
                            Skill_prefab = Hud_Manager
                                .asset_bundle.LoadAsset(name)
                                .TryCast<GameObject>();
                        }
                        else if (Functions.Check_Prefab(name) && name.Contains("topcontent.prefab"))
                        {
                            Details_TopContent_prefab = Hud_Manager
                                .asset_bundle.LoadAsset(name)
                                .TryCast<GameObject>();
                        }
                        else if (
                            Functions.Check_Prefab(name) && name.Contains("bottomcontent.prefab")
                        )
                        {
                            Details_BottomContent_prefab = Hud_Manager
                                .asset_bundle.LoadAsset(name)
                                .TryCast<GameObject>();
                        }
                    }
                }
                loading = false;
                // Only managed statics reference these prefabs, so the scene-load
                // UnloadUnusedAssets pass would destroy them without this flag.
                foreach (
                    GameObject prefab in new GameObject[]
                    {
                        DamageMeter_prefab,
                        Skill_prefab,
                        Details_TopContent_prefab,
                        Details_BottomContent_prefab,
                    }
                )
                {
                    if (!prefab.IsNullOrDestroyed())
                    {
                        prefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                    }
                }
                if (!Loaded())
                {
                    Main.logger_instance.Error(
                        "Damage Meter : damagemeter.prefab or skill.prefab not found in asset bundle"
                    );
                }
            }
        }
    }
}
