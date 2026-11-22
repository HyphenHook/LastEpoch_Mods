using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Data;
using LastEpoch_Hud.Scripts.Core.Crafting;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>One kind of advanced crafting material: what it is called and how it looks in the Advanced Crafting tab.</summary>
internal sealed class AdvancedMaterial
{
    public string Key;
    public string Name;
    public string Description;
    public Color Tint = Color.white; // the colour of the orb's core
    public Color Glow = Color.white; // its light
    public string Effect; // what it does, as one short line
    public string[] Rules = new string[0]; // what the item needs
    public string Flavour = "";
}

/// <summary>The stored orb stacks and the carried orbs available to the Advanced Forge.</summary>
internal static class AdvancedMaterials
{
    public const string AttunementOrb = "AttunementOrb";
    public const string ChaosOrb = "ChaosOrb";
    public const string ReverieOrb = "ReverieOrb";
    public const string EchoOrb = "EchoOrb";
    public const string ReminiscenceOrb = "ReminiscenceOrb";
    public const string EncapsulationOrb = "EncapsulationOrb";

    public static readonly List<AdvancedMaterial> All = new()
    {
        new AdvancedMaterial
        {
            Key = EncapsulationOrb,
            Name = "Orb of Encapsulation",
            Description = "Encapsulates one random affix of the item. An encapsulated affix is part of the item: no "
                + "crafting can change or remove it. The item needs at least four affixes, and can only be encapsulated once.",
            Effect = "Encapsulates a random affix",
            Rules = new[] { "Item needs at least four affixes", "Can only be used once on an item", "Every affix is equally likely" },
            Flavour = "What the orb holds, it keeps.",
            Tint = new Color(0.9f, 0.5f, 0.15f),
            Glow = new Color(1f, 0.7f, 0.3f),
        },
        new AdvancedMaterial
        {
            Key = ReminiscenceOrb,
            Name = "Orb of Reminiscence",
            Description = "Obscures one eligible affix. Unveil the memory at the Advanced Forge and choose a replacement.",
            Effect = "Obscures one affix, then offers up to four same-tier memories",
            Rules = new[] { "Requires at least one unsealed, unencapsulated affix", "Remove the Reminded affix before using another orb",
                "Cannot be used on Legendary, Unique, Set, or Corrupted items" },
            Flavour = "What was forgotten leaves a thread.",
            Tint = new Color(0.62f, 0.39f, 0.96f),
            Glow = new Color(1f, 0.78f, 0.38f),
        },
        new AdvancedMaterial
        {
            Key = EchoOrb,
            Name = "Orb of Echoes",
            Description = "Reveal three new sets of numeric rolls and choose an Echo to keep.",
            Effect = "Rerolls affix and implicit values",
            Rules = new[] { "Keeps affix types, tiers, and protected rolls", "Reopening the Forge restores the same Echoes", "Spent only when you keep an Echo" },
            Flavour = "Three echoes answer. Only one remains.",
            Tint = new Color(0.25f, 0.88f, 0.84f),
            Glow = new Color(0.45f, 1f, 0.93f),
        },
        new AdvancedMaterial
        {
            Key = ReverieOrb,
            Name = "Orb of Reverie",
            Description = "Rerolls every replacement option in an Obscured Memory, including unveiled choices.",
            Effect = "Rerolls all obscured memory options",
            Rules = new[] { "Requires an Obscured Memory", "Usable repeatedly until a memory is kept",
                "Preserves the original tier and prefix or suffix type" },
            Flavour = "Each dream remembers a different truth.",
            Tint = new Color(0.87f, 0.62f, 0.83f),
            Glow = new Color(1f, 0.88f, 0.95f),
        },
        new AdvancedMaterial
        {
            Key = ChaosOrb,
            Name = "Orb of Chaos",
            Description = "Replaces all ordinary, unprotected affixes with new legal affixes.",
            Effect = "Rerolls all eligible affixes into tiers 1–5",
            Rules = new[] { "Keeps prefix and suffix counts", "Preserves Encapsulated, Sealed, and Primordial affixes",
                "Cannot be used on Corrupted, Unique, Legendary, or Set items" },
            Flavour = "Only what is bound survives the storm.",
            Tint = new Color(0.96f, 0.37f, 0.13f),
            Glow = new Color(1f, 0.62f, 0.29f),
        },
        new AdvancedMaterial
        {
            Key = AttunementOrb,
            Name = "Orb of Attunement",
            Description = "Corrupts the item, rerolls implicit, affix, and Unique modifier values, then applies one 0.70–1.30× multiplier.",
            Effect = "Rerolls and attunes numerical values",
            Rules = new[] { "Works on Equipment, Unique, and Legendary items", "One lifetime use per base item",
                "Usable on Corrupted items; corrupts an uncorrupted item",
                "Preserves Encapsulated, Sealed, and Primordial affixes", "Cannot be used on Set items" },
            Flavour = "The stars answer in gold. The void answers in kind.",
            Tint = new Color(0.62f, 0.36f, 0.94f),
            Glow = new Color(1f, 0.77f, 0.29f),
        },
    };

    private static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "Mods", Main.mod_name);
    private static readonly StashMaterialStore Store = new(
        Path.Combine(Folder, "AdvancedMaterials.Stashes.json"), Path.Combine(Folder, "AdvancedMaterials.json"));

    private static string ScopeFor(bool offline, Stash stash)
    {
        if (stash.IsNullOrDestroyed()) return null;
        return StashMaterialStore.ScopeKey(offline, stash.UserIdentity?.MasterAccountID, stash.Id);
    }

    private static string CurrentScope()
    {
        if (!Scenes.IsGameScene()) return null;
        var manager = ItemContainersManager.Instance;
        if (manager.IsNullOrDestroyed() || !manager.IsFullyInitializedAndLoaded) return null;
        var character = PlayerFinder.getPlayerData();
        var tracker = manager.globalDataTracker;
        if (character.IsNullOrDestroyed() || tracker.IsNullOrDestroyed()) return null;
        // The player's resource stash remains authoritative while viewing retired stash tabs.
        return ScopeFor(character.IsOffline, tracker.stash);
    }

    internal static void InitializeStorage()
    {
        try
        {
            Store.Initialize(CurrentScope());
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Advanced Materials storage");
        }
    }

    internal static int StackCount(string key)
    {
        try
        {
            return Store.Count(CurrentScope(), key);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Advanced Materials storage");
            return 0;
        }
    }

    public static int Count(string key)
    {
        try
        {
            string scope = CurrentScope();
            if (scope == null) return 0;
            return (int)Math.Min(int.MaxValue, (long)Store.Count(scope, key) + Drop.OrbInventoryItems.CountCarried(key));
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Advanced Materials storage");
            return 0;
        }
    }

    public static void Add(string key, int amount) => TryCreditStack(key, amount);

    public static bool TryTake(string key)
    {
        try
        {
            string scope = CurrentScope();
            if (scope == null) return false;
            Store.Count(scope, key);
            return Drop.OrbInventoryItems.TrySpendCarried(key) || Store.TryChange(scope, key, -1);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Advanced Materials storage");
            return false;
        }
    }

    internal static bool TryCreditStack(string key, int amount) => amount > 0 && TryChangeStack(key, amount);

    internal static bool TryTakeFromStack(string key, int amount) => amount > 0 && TryChangeStack(key, -amount);

    private static bool TryChangeStack(string key, int change)
    {
        try
        {
            return Store.TryChange(CurrentScope(), key, change);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Advanced Materials storage");
            return false;
        }
    }

    [HarmonyPatch(typeof(StashHolderItemContainer), nameof(StashHolderItemContainer.MergeStashes))]
    private static class MergeMaterialsWithStash
    {
        private sealed class MergeScope
        {
            public string Source;
            public string Target;
        }

        [HarmonyPrefix]
        private static void Prefix(StashItemContainer __1, StashItemContainer __2, out MergeScope __state)
        {
            __state = null;
            try
            {
                var character = PlayerFinder.getPlayerData();
                if (character.IsNullOrDestroyed() || __1.IsNullOrDestroyed() || __2.IsNullOrDestroyed()) return;
                __state = new MergeScope
                {
                    Source = ScopeFor(character.IsOffline, __1.LinkedStash),
                    Target = ScopeFor(character.IsOffline, __2.LinkedStash),
                };
            }
            catch (Exception ex)
            {
                ErrorLog.Report(ex, "Advanced Materials stash merge");
            }
        }

        [HarmonyPostfix]
        private static void Postfix(MergeScope __state)
        {
            if (__state == null) return;
            try
            {
                Store.Merge(__state.Source, __state.Target);
            }
            catch (Exception ex)
            {
                ErrorLog.Report(ex, "Advanced Materials stash merge");
            }
        }
    }
}
