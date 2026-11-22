using System;
using System.Collections.Generic;
using System.Linq;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Settings;

namespace LastEpoch_Hud.Scripts.ModUI.Shell;

/// <summary>Search page index. Page index per HUD; cleared on HUD re-bind (HudLayout.Initialize calls Reset).</summary>
internal static class HudSearch
{
    private static readonly Dictionary<HudPageId, IHudSearchPage> _pages = new();

    public static void Reset() => _pages.Clear();

    public static void Register(IHudSearchPage page)
    {
        if (page == null)
        {
            return;
        }

        _pages[page.PageId] = page;
    }

    public static int Score(string query, string label, string card, string tab, string section) =>
        HudSearchText.ScoreEither(
            query,
            label,
            card,
            tab,
            section,
            LocaleRegistry.Translate(label),
            LocaleRegistry.Translate(card),
            LocaleRegistry.Translate(tab),
            LocaleRegistry.Translate(section)
        );

    public static IReadOnlyList<HudSearchMatch> Find(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<HudSearchMatch>();

        var matches = new List<HudSearchMatch>();
        foreach (KeyValuePair<HudPageId, IHudSearchPage> pair in _pages)
        {
            if (!HudNavigation.TryGetPage(pair.Key, out var section, out var page))
                continue;
            foreach (var entry in pair.Value.SearchEntries)
            {
                string localizedLabel = LocaleRegistry.Translate(entry.Label);
                string localizedCard = LocaleRegistry.Translate(entry.Card);
                string localizedTab = LocaleRegistry.Translate(page.Label);
                string localizedSection = LocaleRegistry.Translate(section.Label);
                int score = Score(query, entry.Label, entry.Card, page.Label, section.Label);
                if (score < 0)
                    continue;
                matches.Add(
                    new HudSearchMatch
                    {
                        Page = pair.Value,
                        PageId = page.Id,
                        Section = localizedSection,
                        Tab = localizedTab,
                        Card = localizedCard,
                        Label = localizedLabel,
                        Score = score,
                    }
                );
            }
        }

        return matches
            .OrderBy(match => match.Score)
            .ThenBy(match => match.Section, StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.Tab, StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.Card, StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static void ClearAll()
    {
        foreach (IHudSearchPage page in _pages.Values)
        {
            page.ClearSearch();
        }
    }
}
