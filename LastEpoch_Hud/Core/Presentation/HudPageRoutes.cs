namespace LastEpoch_Hud.Scripts.Core.ModUI;

/// <summary>Maps a HUD page to its root GameObject name; null when the page has none.</summary>
public static class HudPageRoutes
{
    public static string SearchRoot(HudPageId id) =>
        id switch
        {
            HudPageId.UtilitiesCharacter => "Utilities_Character",
            HudPageId.UtilitiesMultipliers => "Utilities_Multipliers",
            HudPageId.UtilitiesCurrency => "Utilities_Currency",
            HudPageId.UtilitiesBuffs => "Utilities_Buffs",
            HudPageId.UtilitiesHeorot => "Utilities_Heorot",
            HudPageId.BossDropsHeorot => "BossDrops_Heorot",
            HudPageId.UtilitiesQol => "Utilities_QOL",
            HudPageId.ItemsDrop => "Items_Drop",
            HudPageId.ItemsTestDrops => "Items_TestDrops",
            HudPageId.ItemsCraftingSlot => "Items_CraftingSlot",
            HudPageId.ItemsCustomItems => "Items_CustomItems",
            HudPageId.WorldDifficulty => "World_Difficulty",
            HudPageId.WorldMonoliths => "World_Monoliths",
            HudPageId.WorldMisc => "World_Misc",
            HudPageId.WorldCamera => "World_Camera",
            HudPageId.SkillsMinions => "Skills_Minions",
            HudPageId.SkillsCompanions => "Skills_Companions",
            HudPageId.SkillsSummon => "Skills_Summon",
            HudPageId.SkillsQol => "Skills_QOL",
            _ => null,
        };
}
