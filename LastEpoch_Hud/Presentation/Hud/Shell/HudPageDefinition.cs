using System;
using LastEpoch_Hud.Scripts.Core.ModUI;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Shell;

// A page's navigation metadata and runtime lifecycle live together here. Adding a
// page should require one entry, not another branch in HudLayout.
internal sealed class HudPageDefinition
{
    public readonly HudPageId Id;
    public readonly string Label;

    private readonly Action<HudPageId, GameObject, GameObject, Font> _build;
    private readonly Action _show;
    private readonly Action _hide;
    private readonly Action _refresh;

    public HudPageDefinition(
        HudPageId id,
        string label,
        Action<HudPageId, GameObject, GameObject, Font> build,
        Action show,
        Action hide,
        Action refresh = null
    )
    {
        Id = id;
        Label = label;
        _build = build;
        _show = show;
        _hide = hide;
        _refresh = refresh;
    }

    public void Build(GameObject parent, GameObject hud, Font font) =>
        _build?.Invoke(Id, parent, hud, font);

    public void Show() => _show?.Invoke();

    public void Hide() => _hide?.Invoke();

    public void Refresh() => _refresh?.Invoke();
}
