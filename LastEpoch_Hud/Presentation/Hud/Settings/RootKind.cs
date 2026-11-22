namespace LastEpoch_Hud.Scripts.ModUI.Settings;

public enum RootKind
{
    Generic, // bind groups directly to the root, no Tab/Menu
    HudWithTabs, // walks Content + Menu/Content, runs TabManager
}
