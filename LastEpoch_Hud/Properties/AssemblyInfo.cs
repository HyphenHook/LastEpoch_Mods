using System.Reflection;
using System.Runtime.InteropServices;
using MelonLoader;

[assembly: MelonInfo(
    typeof(LastEpoch_Hud.Main),
    LastEpoch_Hud.Main.mod_name,
    LastEpoch_Hud.Main.mod_version,
    "Ash"
)]
[assembly: MelonGame(LastEpoch_Hud.Main.company_name, LastEpoch_Hud.Main.game_name)]
[assembly: MelonColor(1, 150, 20, 175)]
[assembly: VerifyLoaderVersion(0, 6, 0, true)] //If true, check MelonLoder Minimum version

[assembly: AssemblyTitle(LastEpoch_Hud.Main.mod_name)]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct(LastEpoch_Hud.Main.mod_name)]
[assembly: AssemblyCopyright("Copyright ©  2025")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

[assembly: Guid("fdda6fb9-b165-44bb-988a-a6979f74ec43")]

[assembly: AssemblyVersion("5.1.1.0")]
[assembly: AssemblyFileVersion("5.1.1.0")]

[assembly: AssemblyInformationalVersion("5.1.1")]
