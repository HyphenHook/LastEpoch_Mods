# Jewel affix reference (the Idol-copy rows), sorted by idol size

Generated from `Data/JewelAffixes.csv` and the game's own list of idol types per affix (`JewelAffixes.idols.csv`). Percentages are shown as you would type a tooltip number (the CSV stores 0.12 for 12%). A two-stat affix's CSV range sets the first stat in its name; the second scales in proportion.

Idol size matters: the same stat has a bigger range on a bigger idol (for example Cold Resistance is 9–12% on a 1x1 idol and 18–24% on a 2x1 or 1x2). Rows listed as `trade-off` are the big-idol affixes that give one stat and reduce another.



Multipliers apply to ordinary inherited affixes. Corruption-only affixes use balanced native T1 ranges at every tier. Base variable ranges are multiplied by 3 and percentage ranges starting at 1% become 1–5%, with affix-specific overrides in JewelAffixRules.CorruptionRange. T2–T4 increase evenly between T1 and T5.

| Group | T1 | T2 | T3 | T4 | T5 | T6 | T7 |
|---|---|---|---|---|---|---|---|
| Small | 0.8 | 1.1 | 1.4 | 1.7 | 2 | 2.5 | 3 |
| Medium | 0.75 | 1.0125 | 1.275 | 1.5375 | 1.8 | 2.2 | 2.5 |
| Long | 0.7 | 0.95 | 1.2 | 1.45 | 1.7 | 2.1 | 2.4 |
| Big | 0.6 | 0.825 | 1.05 | 1.275 | 1.5 | 1.7 | 2 |

## Small idols (smallest idol it rolls on is 1x1) — 45 affixes

| Id | Title | Affix | Rolls on idols | T1 | T7 | Weight | Note |
|---|---|---|---|---|---|---|---|
| 2117 | of Insulation | Idol Elemental Resistance | 1x1 1x2 1x3 1x4 | 6.4%–16% | 24%–60% | 100 |  |
| 2119 | Blighted | Idol Increased Damage Over Time | 1x1 3x1 | 17.6%–48% | 66%–180% | 100 |  |
| 2136 | Tenacious | Idol Stun Avoidance | 1x1 1x4 | 160–360 | 600–1350 | 100 |  |
| 2826 | Relentless | Increased Damage Over Time and Less Damage Over Time Taken | 1x1 | 8.8%–12% | 33%–45% | 100 |  |
| 2827 | Graceful | Dodge Rating and Increased Dodge Rating | 1x1 | 10.4–20 | 39–75 | 100 |  |
| 2828 | Restful | Increased Health Regeneration and Added Health | 1x1 | 4%–6.4% | 15%–24% | 100 |  |
| 2829 | Serene | Increased Healing Effectiveness and Added Health | 1x1 | 4%–8% | 15%–30% | 100 |  |
| 2830 | Loom Walker's | Increased Stun Chance and Added Stun Avoidance | 1x1 | 16.8%–24% | 63%–90% | 100 |  |
| 2831 | Dreaded | Void Penetration and Minion Void Penetration | 1x1 | 0.8%–1.6% | 3%–6% | 100 |  |
| 2832 | Widow's | Poison Penetration and Minion Poison Penetration | 1x1 | 0.8%–1.6% | 3%–6% | 100 |  |
| 2833 | Fanged | Physical Penetration and Minion Physical Penetration | 1x1 | 0.8%–1.6% | 3%–6% | 100 |  |
| 2834 | Fateweaver's | Necrotic Penetration and Minion Necrotic Penetration | 1x1 | 0.8%–1.6% | 3%–6% | 100 |  |
| 2835 | of Phobia | Void Resistance and Minion Void Resistance | 1x1 | 7.2%–9.6% | 27%–36% | 100 |  |
| 2836 | of Antivenom | Poison Resistance and Minion Poison Resistance | 1x1 | 7.2%–9.6% | 27%–36% | 100 |  |
| 2837 | of Chitin | Physical Resistance and Minion Physical Resistance | 1x1 | 7.2%–9.6% | 27%–36% | 100 |  |
| 2838 | of Silk | Necrotic Resistance and Minion Necrotic Resistance | 1x1 | 7.2%–9.6% | 27%–36% | 100 |  |
| 2839 | of the Briar | Damage Reflected and Damage Dealt to Attackers | 1x1 | 4%–12% | 15%–45% | 100 |  |
| 2840 | of the Trapdoor | Critical Strike Avoidance and Increased Critical Strike Chance | 1x1 | 1.6%–3.2% | 6%–12% | 100 |  |
| 2841 | of Needles | Reduced Bonus Damage Taken from Crits and Increased Critical Strike Chance | 1x1 | 1.6%–3.2% | 6%–12% | 100 |  |
| 2842 | Armored | Armor and Increased Armor | 1x1 | 22.4–32 | 84–120 | 100 |  |
| 2843 | Inspired | Mana and Increased Mana Regen | 1x1 | 6.4–9.6 | 24–36 | 100 |  |
| 2844 | Jumping Spider's | Added Stun Avoidance and Increased Stun Immunity Duration | 1x1 | 62.4–80 | 234–300 | 100 |  |
| 2845 | Quenching | Ward and Ward Decay Threshold on Potion Use | 1x1 | 12–20 | 45–75 | 100 |  |
| 2846 | Preserving | Ward Retention and Ward Per Second | 1x1 | 4%–5.6% | 15%–21% | 100 |  |
| 2847 | Biting | Cold Penetration and Minion Cold Penetration | 1x1 | 0.8%–1.6% | 3%–6% | 100 |  |
| 2848 | Scorching | Fire Penetration and Minion Fire Penetration | 1x1 | 0.8%–1.6% | 3%–6% | 100 |  |
| 2849 | Illuminating | Lightning Penetration and Minion Lightning Penetration | 1x1 | 0.8%–1.6% | 3%–6% | 100 |  |
| 2850 | of Refuge | Cold Resistance and Minion Cold Resistance | 1x1 | 7.2%–9.6% | 27%–36% | 100 |  |
| 2851 | of Many Threads | Elemental Resistance and Minion Elemental Resistance | 1x1 | 3.2%–4.8% | 12%–18% | 100 |  |
| 2852 | of the Salamander | Fire Resistance and Minion Fire Resistance | 1x1 | 7.2%–9.6% | 27%–36% | 100 |  |
| 2853 | of Respite | Health and Added Health Regeneration | 1x1 | 11.2–14.4 | 42–54 | 100 |  |
| 2854 | of Repose | Health and Mana | 1x1 | 11.2–14.4 | 42–54 | 100 |  |
| 2855 | of Golden Light | Lightning Resistance and Minion Lightning Resistance | 1x1 | 7.2%–9.6% | 27%–36% | 100 |  |
| 2856 | of the Tarantula | Endurance and Endurance Threshold | 1x1 | 0.8%–1.6% | 3%–6% | 100 |  |
| 2857 | of Revelry | Increased Chance to Find Potions and Health Gained on Potion Use | 1x1 | 4%–8% | 15%–30% | 100 |  |
| 3063 | Shredding | Slow chance converted to Armor Shred chance | 1x1 | 100%–100% | 100%–100% | 0 | corruption only; same balanced range at every tier |
| 3064 | Slippery | Minion Poison Resistance | 1x1 | 6%–15% | 6%–15% | 100 | corruption only; same balanced range at every tier |
| 3065 | Ironclad | Chance to Slow on Hit | 1x1 | 9%–15% | 9%–15% | 100 | corruption only; same balanced range at every tier |
| 3066 | Vital | Reduced Bonus Damage Taken from Critical Strikes | 1x1 | 1%–5% | 1%–5% | 100 | corruption only; same balanced range at every tier |
| 3067 | Contemplative | Critical Strike Avoidance | 1x1 | 6%–9% | 6%–9% | 100 | corruption only; same balanced range at every tier |
| 3068 | Overflowing | Chance to apply Frailty on Minion Hit | 1x1 | 6%–12% | 6%–12% | 100 | corruption only; same balanced range at every tier |
| 3069 | Hearty | Chance to apply Frailty on Hit | 1x1 | 9%–15% | 9%–15% | 100 | corruption only; same balanced range at every tier |
| 3070 | Shrouded | All Resistances for you and your Minions | 1x1 2x1 1x2 3x1 1x3 4x1 1x4 2x2 | 1%–3% | 1%–3% | 0 | corruption only; same balanced range at every tier |
| 3071 | Bees per 10 Seconds | Bees per 10 Seconds | 1x1 2x1 1x2 3x1 1x3 4x1 1x4 2x2 | 1–3 | 1–3 | 0 | corruption only; same balanced range at every tier |
| 3072 | Buzzing | Elemental Bees per 10 Seconds | 1x1 2x1 1x2 3x1 1x3 4x1 1x4 2x2 | 1–3 | 1–3 | 0 | corruption only; same balanced range at every tier |

## Medium idols (smallest is 1x2 / 2x1) — 45 affixes

| Id | Title | Affix | Rolls on idols | T1 | T7 | Weight | Note |
|---|---|---|---|---|---|---|---|
| 2106 | Sanctified | Health on Kill | 2x1 2x2 | 7.5–15 | 25–50 | 100 |  |
| 2107 | Vigorous | Idol Increased Health | 1x2 | 3.75%–8.25% | 12.5%–27.5% | 100 |  |
| 2134 | Assassin's | Idol Increased Critical Strike Chance | 1x2 1x4 | 15%–41.25% | 50%–137.5% | 100 |  |
| 2858 | Wolf Spider's | Chance to Inflict Bleed on Hit and Physical Penetration with Bleed | 2x1 | 11.25%–15% | 37.5%–50% | 100 |  |
| 2859 | Recluse's | Chance to Poison on Hit and Poison Penetration with Poison | 2x1 | 11.25%–15% | 37.5%–50% | 100 |  |
| 2860 | Predator's | Health on Kill and Added Health | 2x1 | 6.75–15 | 22.5–50 | 100 |  |
| 2861 | Paralysing | Increased Stun Duration and Increased Stun Chance | 2x1 | 12%–15% | 40%–50% | 100 |  |
| 2862 | Swaddling | Vitality and Added Health Regen | 2x1 | 3–3.75 | 10–12.5 | 100 |  |
| 2863 | Fateweaver's | Necrotic Penetration and Minion Necrotic Penetration | 2x1 | 2.25%–3% | 7.5%–10% | 100 |  |
| 2864 | Dreaded | Void Penetration and Minion Void Penetration | 2x1 | 2.25%–3% | 7.5%–10% | 100 |  |
| 2865 | Widow's | Poison Penetration and Minion Poison Penetration | 2x1 | 2.25%–3% | 7.5%–10% | 100 |  |
| 2866 | Fanged | Physical Penetration and Minion Physical Penetration | 2x1 | 2.25%–3% | 7.5%–10% | 100 |  |
| 2867 | of Silk | Necrotic Resistance and Minion Necrotic Resistance | 2x1 | 13.5%–18% | 45%–60% | 100 |  |
| 2868 | of Phobia | Void Resistance and Minion Void Resistance | 2x1 | 13.5%–18% | 45%–60% | 100 |  |
| 2869 | of Antivenom | Poison Resistance and Minion Poison Resistance | 2x1 | 13.5%–18% | 45%–60% | 100 |  |
| 2870 | of Chitin | Physical Resistance and Minion Physical Resistance | 2x1 | 13.5%–18% | 45%–60% | 100 |  |
| 2871 | of the Hunter | Added Bow Damage and Increased Bow Damage | 2x1 | 1.5–3.75 | 5–12.5 | 100 |  |
| 2872 | of Clotho | Added Melee Damage and Increased Melee Damage | 2x1 | 2.25–4.5 | 7.5–15 | 100 |  |
| 2873 | of Atropos | Added Throwing Damage and Increased Throwing Damage | 2x1 | 0.75–2.25 | 2.5–7.5 | 100 |  |
| 2874 | of Lachesis | Added Spell Damage and Increased Spell Damage | 2x1 | 1.5–3 | 5–10 | 100 |  |
| 2875 | Gilded | Chance to Gain 30 Ward When Hit and Ward Decay Threshold | 1x2 | 12%–22.5% | 40%–75% | 100 |  |
| 2876 | Lava Gatherer's | Chance to Ignite on Hit and Fire Penetration with Ignite | 1x2 | 11.25%–15% | 37.5%–50% | 100 |  |
| 2877 | Apostle's | Chance to Shock on Hit and Increased Shock Duration | 1x2 | 15%–22.5% | 50%–75% | 100 |  |
| 2878 | Severing | Increased Critical Strike Chance and Critical Strike Multiplier | 1x2 | 16.5%–22.5% | 55%–75% | 100 |  |
| 2879 | Winternid's | Freeze Rate Multiplier and Ward when you Freeze an Enemy | 1x2 | 12%–18.75% | 40%–62.5% | 100 |  |
| 2880 | Vanquisher's | Health on Kill and Increased Health | 1x2 | 6.75–15 | 22.5–50 | 100 |  |
| 2881 | Venom Drinker's | Potion Health Converted to Ward and Ward Gained on Potion use | 1x2 | 16.5%–18.75% | 55%–62.5% | 100 |  |
| 2882 | Biting | Cold Penetration and Minion Cold Penetration | 1x2 | 2.25%–3% | 7.5%–10% | 100 |  |
| 2883 | Scorching | Fire Penetration and Minion Fire Penetration | 1x2 | 2.25%–3% | 7.5%–10% | 100 |  |
| 2884 | Illuminating | Lightning Penetration and Minion Lightning Penetration | 1x2 | 2.25%–3% | 7.5%–10% | 100 |  |
| 2885 | of Refuge | Cold Resistance and Minion Cold Resistance | 1x2 | 13.5%–18% | 45%–60% | 100 |  |
| 2886 | of Many Threads | Elemental Resistance and Minion Elemental Resistance | 1x2 | 6%–9% | 20%–30% | 100 |  |
| 2887 | of the Salamander | Fire Resistance and Minion Fire Resistance | 1x2 | 13.5%–18% | 45%–60% | 100 |  |
| 2888 | of Golden Light | Lightning Resistance and Minion Lightning Resistance | 1x2 | 13.5%–18% | 45%–60% | 100 |  |
| 2889 | of the Firstborn | Added Block Effectiveness and Health Gained on Block | 1x2 | 30–60 | 100–200 | 100 |  |
| 2890 | of the Loom | Ward Per Second and Ward Retention | 1x2 | 9–12 | 30–40 | 100 |  |
| 2891 | of the Web | Increased Damage Over Time and Chance to Apply Frailty on Hit | 1x2 | 10.5%–18.75% | 35%–62.5% | 100 |  |
| 3055 | Plated | Chance to Chill on Hit | 1x2 2x1 | 15%–30% | 15%–30% | 100 | corruption only; same balanced range at every tier |
| 3056 | Shepherd's | Chance to Chill on Minion Hit | 1x2 2x1 | 15%–30% | 15%–30% | 100 | corruption only; same balanced range at every tier |
| 3057 | Summoner's | Increased Critical Strike Chance | 1x2 2x1 | 12.000001%–24% | 12.000001%–24% | 100 | corruption only; same balanced range at every tier |
| 3058 | Lone Wolf's | Added Health Regen | 1x2 2x1 | 6–12 | 6–12 | 100 | corruption only; same balanced range at every tier |
| 3059 | Festering | Added Dodge Rating while using Evade | 1x2 2x1 | 15–42 | 15–42 | 100 | corruption only; same balanced range at every tier |
| 3060 | Piercing | Minion Void Resistance | 1x2 2x1 | 12.000001%–30% | 12.000001%–30% | 100 | corruption only; same balanced range at every tier |
| 3061 | Grounded | Lightning Damage taken as Physical | 1x2 | 6%–9% | 6%–9% | 0 | corruption only; same balanced range at every tier |
| 3062 | Hardened | Necrotic Damage taken as Physical | 2x1 | 6%–9% | 6%–9% | 0 | corruption only; same balanced range at every tier |

## Long idols (smallest is 1x3 / 3x1) — 11 affixes

| Id | Title | Affix | Rolls on idols | T1 | T7 | Weight | Note |
|---|---|---|---|---|---|---|---|
| 2892 | of the Keen Edge | Increased Critical Strike Chance and Added Critical Strike Multiplier | 3x1 1x3 4x1 1x4 2x2 | 0.7%–1.4% | 2.4%–4.8% | 0 | never rolls (Idol weight 0) |
| 2895 | of the Carapace | Increased Armor and Reduced bonus damage taken from critical hits | 3x1 1x3 | 0.7%–0.7% | 2.4%–2.4% | 0 | never rolls (Idol weight 0) |
| 2897 | of Tempering | Ward decay threshold and Ward per second | 3x1 1x3 4x1 1x4 2x2 | 0.7–1.4 | 2.4–4.8 | 0 | never rolls (Idol weight 0) |
| 2899 | of Attrition | Increased damage over time and Endurance | 3x1 1x3 4x1 1x4 2x2 | 0.7%–0.7% | 2.4%–2.4% | 0 | never rolls (Idol weight 0) |
| 3048 | Steadfast | Increased Mana Regen | 1x3 3x1 | 9%–15% | 9%–15% | 100 | corruption only; same balanced range at every tier |
| 3049 | Brimming | Minion Physical Resistance | 1x3 3x1 | 15%–45% | 15%–45% | 100 | corruption only; same balanced range at every tier |
| 3050 | Stalwart | Cooldown Recovery Speed for Evade | 1x3 3x1 | 6%–15% | 6%–15% | 100 | corruption only; same balanced range at every tier |
| 3051 | Mending | Ward per Second | 1x3 3x1 | 22.5–52.5 | 22.5–52.5 | 100 | corruption only; same balanced range at every tier |
| 3052 | Shimmering | Added Critical Strike Multiplier | 1x3 3x1 | 9%–18% | 9%–18% | 100 | corruption only; same balanced range at every tier |
| 3053 | Precise | Increased Armor | 1x3 3x1 | 6%–15% | 6%–15% | 100 | corruption only; same balanced range at every tier |
| 3054 | Merciless | Added Stun Avoidance | 1x3 3x1 | 75–120 | 75–120 | 100 | corruption only; same balanced range at every tier |

## Big idols only (1x4 / 4x1 / 2x2) — 27 affixes

| Id | Title | Affix | Rolls on idols | T1 | T7 | Weight | Note |
|---|---|---|---|---|---|---|---|
| 2893 | of Renewal | Increased Cooldown Recovery Speed and Increased Mana Regeneration | 4x1 1x4 | 0.6%–0.6% | 2%–2% | 0 | never rolls (Idol weight 0) |
| 2894 | of the Hunt | Increased Movement Speed and Increased Damage | 4x1 1x4 | 0.6%–0.6% | 2%–2% | 0 | never rolls (Idol weight 0) |
| 2896 | of Evasion | Added Dodge rating if you have been hit recently and Critical Strike Avoidance | 2x2 | 3–9 | 10–30 | 0 | never rolls (Idol weight 0) |
| 2898 | of Thrift | Chance to not consume Potion on use and Ward gained on potion use | 4x1 1x4 | 0.6%–0.6% | 2%–2% | 0 | never rolls (Idol weight 0) |
| 3029 | Focused | Increased Effect of Frenzy on You | 2x2 | 20%–30% | 20%–30% | 100 | corruption only; same balanced range at every tier |
| 3030 | Disciplined | Increased Effect of Haste on You | 2x2 | 20%–30% | 20%–30% | 100 | corruption only; same balanced range at every tier |
| 3031 | Slinger's | Chill Immunity | 2x2 | 100%–100% | 100%–100% | 100 | corruption only; same balanced range at every tier |
| 3032 | Bracing | Block Effectiveness and Block Chance | 4x1 1x4 | 120–240 | 120–240 | 100 | corruption only; block chance 2–10%; same ranges at every tier |
| 3033 | Marksman's | Minion Elemental Resistance | 4x1 1x4 | 15%–30% | 15%–30% | 100 | corruption only; same balanced range at every tier |
| 3034 | Archer's | Ward gained when you Dodge | 4x1 1x4 | 75–200 | 75–200 | 100 | corruption only; same balanced range at every tier |
| 3035 | Sharpshooter's | Poison Immunity for Minions | 4x1 1x4 | 100%–100% | 100%–100% | 100 | corruption only; same balanced range at every tier |
| 3036 | Hunter's | Bleed Immunity for Minions | 4x1 1x4 | 100%–100% | 100%–100% | 100 | corruption only; same balanced range at every tier |
| 3037 | Sorcerer's | Added Bow Damage | 4x1 1x4 | 9–44 | 9–44 | 100 | corruption only; same balanced range at every tier |
| 3038 | Spellbound | Added Spell Damage | 4x1 1x4 | 12–44 | 12–44 | 100 | corruption only; same balanced range at every tier |
| 3039 | Mystic's | Added Melee Damage | 4x1 1x4 | 15–50 | 15–50 | 100 | corruption only; same balanced range at every tier |
| 3040 | Brawler's | Increased Mana Regen per 100 Maximum Mana | 4x1 1x4 | 1%–5% | 1%–5% | 100 | corruption only; same balanced range at every tier |
| 3041 | Duelist's | Added Throwing Damage | 4x1 1x4 | 9–44 | 9–44 | 100 | corruption only; same balanced range at every tier |
| 3042 | Warrior's | Added Minion Critical Strike Multiplier | 4x1 1x4 | 15%–45% | 15%–45% | 100 | corruption only; same balanced range at every tier |
| 3043 | Warded | Slow Immunity | 2x2 | 100%–100% | 100%–100% | 100 | corruption only; same balanced range at every tier |
| 3044 | Frenzied | Minion Chill Immunity | 2x2 | 100%–100% | 100%–100% | 100 | corruption only; same balanced range at every tier |
| 3045 | Hasty | Minion Critical Strike Chance | 2x2 | 1%–2% | 1%–2% | 100 | corruption only; same balanced range at every tier |
| 3046 | Maddened | Health Regen also appllies to Ward | 2x2 | 6%–9% | 6%–9% | 100 | corruption only; same balanced range at every tier |
| 3047 | Fleet | Health gained when you Dodge | 2x2 | 45–200 | 45–200 | 100 | corruption only; same balanced range at every tier |
| 3112 | Rapt | Seconds of Frenzy after you use Evade | 2x2 | 1–4 | 1–4 | 100 | corruption only; same balanced range at every tier |
| 3150 | Heretic's | Strength per Equipped Heretical Idol | 2x2 | 2.4–3.6 | 8–12 | 100 | per Equipped Heretical Idol |
| 3151 | Zealot's | Dexterity per Equipped Heretical Idol | 2x2 | 2.4–3.6 | 8–12 | 100 | per Equipped Heretical Idol |
| 3152 | Penitent's | Vitality per Equipped Heretical Idol | 2x2 | 2.4–3.6 | 8–12 | 100 | per Equipped Heretical Idol |
