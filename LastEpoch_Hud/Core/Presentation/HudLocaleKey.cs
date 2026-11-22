using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.ModUI;

/// <summary>
/// Maps legacy spelling and renamed HUD captions to the stable locale key that owns
/// their translation. Keeping the aliases here lets runtime localization and tests
/// share one source of truth without duplicating equivalent strings in every locale.
/// </summary>
public static class HudLocaleKey
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["Ability Experience Multiplier"] = "Ability Multiplier",
        ["Add All Glyphs x99"] = "Add all Glyphs x99",
        ["Add All Runes x99"] = "Add all Runes x99",
        ["Add All Affixs x10"] = "Add All Affixes x10",
        ["Added Golems Per 4 Skeletons"] = "Quantity per 4 Skeletons",
        ["Additional Mages From Items"] = "Summon Quantity from Items",
        ["Additional Mages From Passives"] = "Summon Quantity from Passive",
        ["Additional Mages From Skill Tree"] = "Summon Quantity from Skill Tree",
        ["Additional Mages Per Cast"] = "Summon Quantity per Cast",
        ["Additional Skeletons From Passives"] = "Summon Quantity from Passive",
        ["Additional Skeletons From Skill Tree"] = "Summon Quantity from Skill Tree",
        ["Additional Skeletons Per Cast"] = "Summon Quantity per Cast",
        ["Affix Tier"] = "Affix Tiers",
        ["Affix Value"] = "Affix Values",
        ["Affixs"] = "Affixes",
        ["Auto-Cast"] = "AutoCast",
        ["Auto-Pickup"] = "Auto Pickup",
        ["Auto-Sell"] = "Auto Sell",
        ["Auto-Store"] = "Auto Store Materials",
        ["Cap Player Level Gain To Zone Level"] = "Cap Player Level Gain to Zone Level",
        ["Chance For Two Extra Projectiles"] = "Chance for 2 extra Projectiles",
        ["Chance To Cast From Minion Death"] = "Chance to cast from minion death",
        ["Chance To Resummon On Death"] = "Chance to Resummon On Death",
        ["Choose Masterie"] = "Choose Mastery",
        ["Currency"] = "Currencies",
        ["Delayed Wraiths"] = "Delayed",
        ["Disable Limit To 2 Wraiths"] = "Disable Limit to 2",
        ["Disable Simple Path Requirement"] = "Disable Simple Path",
        ["Don't Stop When Out Of Mana"] = "Don't stop when Out of Mana",
        ["Dread Shades"] = "DreadShades",
        ["Experience Tomes"] = "XpTome",
        ["Favor"] = "Favour",
        ["Force Death Knight"] = "Force DeathKnight",
        ["Force Set"] = "Set",
        ["Force Unique"] = "Unique",
        ["Forgin"] = "Forging",
        ["Forgin Potencial"] = "Forging Potential",
        ["Health Regeneration"] = "Health Regen",
        ["Hide Material Notifications"] = "Hide Materials Notifications",
        [
            "Hold the modifier key and press the skill's normal keybind to toggle auto-cast on or off."
        ] = "Hold the modifier key and press the skill's normal keybind to toggle autocast on/off.",
        ["Immune During Movement"] = "Immune during Movement",
        ["Implicit Value"] = "Implicits Values",
        ["Increased Cast Speed"] = "Cast Speed",
        ["Increased Fire Aura Area"] = "Increase Fire Aura",
        ["Increased Movement Speed"] = "Increase Move Speed",
        ["Infernal Shade Chance On Death"] = "Infernal Shade On Death",
        ["Items In Filter"] = "From Filter (Show rule)",
        ["Items Not In Filter"] = "Items (Hide rule)",
        ["Legendary Potencial"] = "Legendary Potential",
        ["Level Up to level 100"] = "Level Up To Level 100",
        ["Load On Start"] = "Load OnStart",
        ["Mana Regeneration"] = "Mana Regen",
        ["Mana Regeneration While Channeling"] = "Mana Regen when Channeling",
        ["Marrow Shards Chance On Death"] = "Marrow Shards On Death",
        ["Movement Skills"] = "Movement skills",
        ["No Stability Lost When You Die"] = "No Lost When Die",
        ["No Target Required"] = "No Target",
        ["Pause Auto-Cast In Non-Combat Zones"] = "Pause autocast in non-combat zones",
        ["Pause Auto-Cast On Zone Change"] =
            "Pause autocast on zone change (press any skill to resume)",
        ["Press a key..."] = "Press any key…",
        ["Raise Zone Level To Match Player"] = "Raise Zone Level to Match Player",
        ["Reduced Decay"] = "Decay",
        ["Remove Fog Of War"] = "Remove Fog of War",
        ["Remove Mana Cost While Channeling"] = "Remove Channel Cost",
        ["Save Character Data"] = "Save",
        ["Self Resurect Chance"] = "Self Resurrect Chance",
        ["Skip Campaign"] = "Complete Main Quest",
        ["Stability = Max Stability On Stability Change"] =
            "Stability = Max Stability (append on Stability Changed)",
        ["Strenght"] = "Strength",
        ["Summon Quantity from Skil lTree"] = "Summon Quantity from Skill Tree",
        ["Summon To Maximum"] = "Summon up to max",
        ["The options above must be set before entering a Monolith."] =
            "All the above options need to be set before entering Monoliths (append on Start)",
        ["Twins"] = "Enable Twins",
        ["Undead Armor Aura"] = "Increase Armor Aura",
        ["Undead Movement Speed Aura"] = "Increase Move Speed Aura",
        ["Unique Mod Value"] = "Unique Mods",
        ["Unlock All Idol Slots"] = "Unlock all Idols Slots",
        ["Wolfs"] = "Wolves",
        ["Wraiths Do Not Decay"] = "Disable Decay",
        ["Zoom Per Scroll"] = "Zoom per Scroll",
    };

    public static IReadOnlyDictionary<string, string> KnownAliases => Aliases;

    public static string Canonicalize(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        return Aliases.TryGetValue(key, out string canonical) ? canonical : key;
    }
}
