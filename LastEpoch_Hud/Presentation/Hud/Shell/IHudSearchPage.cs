using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.ModUI;

namespace LastEpoch_Hud.Scripts.ModUI.Shell;

internal interface IHudSearchPage
{
    HudPageId PageId { get; }
    IReadOnlyList<HudSearchEntry> SearchEntries { get; }
    void ApplySearch(string query);
    void ClearSearch();
}
