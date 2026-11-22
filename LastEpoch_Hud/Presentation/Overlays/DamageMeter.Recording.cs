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

    public static float Dps(float damage)
    {
        return RecordedSeconds > 0f ? damage / RecordedSeconds : 0f;
    }

    public static string FormatNumber(float value)
    {
        if (value >= 1000000f)
        {
            return (value / 1000000f).ToString("0.00") + "M";
        }
        if (value >= 10000f)
        {
            return (value / 1000f).ToString("0.0") + "k";
        }
        return System.Convert.ToInt32(value).ToString();
    }

    public class DamageDeal
    {
        [HarmonyPatch(typeof(AbilityEventListener), "DetailedAbilityEvent")]
        public class AbilityEventListener_DetailedAbilityEvent
        {
            [HarmonyPostfix]
            static void Postfix(DetailedAbilityEvent __0)
            {
                if ((Scenes.IsGameScene()) && (On))
                {
                    bool target_is_player = false;
                    if (__0.target == Refs_Manager.player_actor)
                    {
                        target_is_player = true;
                    }
                    bool target_is_summon = false;
                    var summons = PlayerSummons();
                    if (summons != null)
                    {
                        foreach (Summoned summoned in summons)
                        {
                            if (summoned.actor == __0.target)
                            {
                                target_is_summon = true;
                                break;
                            }
                        }
                    }
                    if ((!target_is_player) && (!target_is_summon))
                    {
                        Record(
                            __0.ability.abilityName,
                            __0.damageDealt,
                            __0.hit,
                            __0.crit,
                            __0.kill,
                            __0.overkill,
                            __0.target.name,
                            null
                        );
                    }
                }
            }
        }

        static void Record(
            string name,
            float damage,
            bool hit,
            bool crit,
            bool kill,
            float overkill,
            string target_name,
            Sprite icon
        )
        {
            TotalDamageDeal += damage;
            foreach (Skill skill in Skills)
            {
                if (skill.AbilityName == name)
                {
                    skill.Damages.Add(damage);
                    skill.Hits.Add(hit);
                    skill.Crits.Add(crit);
                    skill.Kills.Add(kill);
                    skill.Overkills.Add(overkill);
                    skill.TargetName.Add(target_name);
                    return;
                }
            }
            UI.AddSkill(name, damage, hit, crit, kill, overkill, target_name, icon);
        }

        static bool IsPlayerOrSummon(Actor actor)
        {
            if (actor.IsNullOrDestroyed())
            {
                return false;
            }
            if (actor == Refs_Manager.player_actor)
            {
                return true;
            }
            var summons = PlayerSummons();
            if (summons != null)
            {
                foreach (Summoned summoned in summons)
                {
                    if (summoned.actor == actor)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // Ailment ticks never raise DetailedAbilityEvent; they reach ProtectionClass.ApplyDamage with the
        // ActiveAilment as their DamageSource. ApplyDamage's ValueTuple result does not marshal through the
        // detour, so the amount is read from the BaseHealth.HealthDamage call it makes on the target.
        // Ticks arrive every frame per target, so they are summed here and written to the rows by
        // FlushAilments rather than stored one by one.
        struct PendingAilment
        {
            public Ailment Ailment;
            public float Damage;
            public string TargetName;
        }

        static readonly System.Collections.Generic.Dictionary<
            string,
            PendingAilment
        > pending_ailments = new System.Collections.Generic.Dictionary<string, PendingAilment>();
        static readonly System.Collections.Generic.Dictionary<
            string,
            LoadRef<Sprite>
        > ailment_icons = new System.Collections.Generic.Dictionary<string, LoadRef<Sprite>>();
        static bool ailment_logged = false;
        static Ailment current_ailment = null;
        static BaseHealth current_target = null;

        [HarmonyPatch(typeof(ProtectionClass), "ApplyDamage")]
        public class ProtectionClass_ApplyDamage
        {
            [HarmonyPrefix]
            static void Prefix(ProtectionClass __instance, DamageSource __1)
            {
                current_ailment = null;
                current_target = null;
                if (!On || __1 == null || !Scenes.IsGameScene())
                {
                    return;
                }
                var info = __1.GetDamageSourceInfo();
                Ailment ailment = info.Item1;
                if (ailment.IsNullOrDestroyed())
                {
                    return;
                } //Hits are counted by DetailedAbilityEvent
                Actor target = __instance.actor;
                if (
                    target.IsNullOrDestroyed()
                    || !IsPlayerOrSummon(info.Item5)
                    || IsPlayerOrSummon(target)
                )
                {
                    return;
                }
                current_ailment = ailment;
                current_target = target.health;
            }

            [HarmonyPostfix]
            static void Postfix()
            {
                current_ailment = null;
                current_target = null;
            }
        }

        [HarmonyPatch(typeof(BaseHealth), "HealthDamage")]
        public class BaseHealth_HealthDamage
        {
            [HarmonyPrefix]
            static void Prefix(BaseHealth __instance, float __0)
            {
                if (current_ailment == null || __0 <= 0f || __instance != current_target)
                {
                    return;
                }
                string name = AilmentName(current_ailment);
                if (!ailment_logged)
                {
                    ailment_logged = true;
                    Main.logger_instance.Msg(
                        "Damage Meter : first ailment tick " + name + " damage=" + __0
                    );
                }
                pending_ailments.TryGetValue(name, out PendingAilment pending);
                pending.Ailment = current_ailment;
                pending.Damage += __0;
                pending.TargetName = __instance.gameObject.name;
                pending_ailments[name] = pending;
            }
        }

        static string AilmentName(Ailment ailment)
        {
            string name = ailment.displayName;
            if (string.IsNullOrEmpty(name))
            {
                name = ailment.name;
            }
            return name ?? "Ailment";
        }

        static Sprite AilmentIcon(string name, Ailment ailment)
        {
            if (!ailment_icons.TryGetValue(name, out LoadRef<Sprite> loadRef))
            {
                SoftRef<Sprite> softRef = ailment.getIcon();
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
                ailment_icons[name] = loadRef;
            }
            return loadRef.AssetOrNull;
        }

        public static void FlushAilments()
        {
            if (pending_ailments.Count == 0)
            {
                return;
            }
            foreach (var entry in pending_ailments)
            {
                Record(
                    entry.Key,
                    entry.Value.Damage,
                    false,
                    false,
                    false,
                    0f,
                    entry.Value.TargetName,
                    AilmentIcon(entry.Key, entry.Value.Ailment)
                );
            }
            pending_ailments.Clear();
        }

        public static void ClearPending()
        {
            pending_ailments.Clear();
        }
    }

    public struct Skill
    {
        public GameObject Obj;
        public Sprite Icon;
        public string AbilityName;
        public System.Collections.Generic.List<float> Damages;
        public System.Collections.Generic.List<bool> Hits;
        public System.Collections.Generic.List<bool> Crits;
        public System.Collections.Generic.List<bool> Kills;
        public System.Collections.Generic.List<float> Overkills;
        public System.Collections.Generic.List<string> TargetName;
    }
}
