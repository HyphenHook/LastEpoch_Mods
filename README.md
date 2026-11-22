# Last Epoch HUD

Working Season 5 version of [Ash's Last Epoch HUD](https://github.com/RCInet/LastEpoch_Mods).

This mod is offline only.

In a zone, press **F3** to open and close the mod menu. Escape closes it.

## Install

**Automatic: Use this repo to install the mod: https://github.com/TriSSec-Lab/LE-Hud-Installer**

Manual:

1. Install [MelonLoader 0.7.3 Open-Beta](https://github.com/LavaGang/MelonLoader/releases). Point the installer at the game folder.
2. Start the game once, wait until the main menu, then close it.
3. From the [latest release](https://github.com/Syncingoutt/LastEpoch_Mods/releases), download `LastEpoch_Hud.zip` and `UnityEngine.CoreModule.dll`
4. Put `LastEpoch_Hud.dll` and `LastEpoch_Hud` directly in the game `Mods` folder.

   ```text
   <Last Epoch>\Mods\LastEpoch_Hud.dll
   <Last Epoch>\Mods\LastEpoch_Hud\Assets\lastepochmods
   ```

   The DLL stays next to the `LastEpoch_Hud` folder, not inside it.

5. Replace the downloaded `UnityEngine.CoreModule.dll` in this folder:

   ```text
   <Last Epoch>\MelonLoader\Il2CppAssemblies\
   ```

## Update

1. From the [latest release](https://github.com/Syncingoutt/LastEpoch_Mods/releases), download `LastEpoch_Hud.zip` and `UnityEngine.CoreModule.dll`.
2. Extract `LastEpoch_Hud.zip` into `<Last Epoch>\Mods`, replacing the old files.
3. If the game itself was updated: launch it once, wait for the main menu, then quit. MelonLoader rebuilds its files and overwrites `UnityEngine.CoreModule.dll`.
4. Copy the downloaded `UnityEngine.CoreModule.dll` into this folder, replacing the existing one:

   ```text
   <Last Epoch>\MelonLoader\Il2CppAssemblies\
   ```


## In case of issues

1. Delete mods, delete melonloader
2. Head here: https://melon-loader.com/ (or github) and download MelonLoader 0.7.3
3. Launch the game once
4. Head into https://github.com/Syncingoutt/LastEpoch_Mods/releases/tag/v4.4.12 and download the zip and the .dll file
5. Add the LastEpoch_Hud.dll and folder into the Mods folder
6. Replace the UnityEngine.CoreModule.dll inside MelonLoader\Il2CppAssemblies
7. Head into the game and check if it works keybind F3

In case this does not work, please contact me on Discord: sync0333 (attach a log file from MelonLoader/latest.log)

## Linux

1. Check if your LastEpoch_Hud folder looks like this:
<img width="927" height="208" alt="image" src="https://github.com/user-attachments/assets/dd848129-0927-47c9-b330-5fd5af2b7992" />

2. If that is the case, it got unzipped badly, simply create 2 new folders (Locales | Assets)
3. Rename the files (e.g LastEpoch_Hud\Locales\base.json -> base.json)
4. Move the new files into the respective folders:
* Locales - base.json | en.json | fr.json | ko.json | zh.json
* Assets - lastepochmods
5. Head into the game and see if it works (F3)

## Optional translations

The HUD follows the language selected in the game. Without a matching translation, it uses English fallback.

Available translations are: ```fr, ko, zh```

Start the game and select your language under Settings → Gameplay → Interface → Language. The mod HUD follows that setting.

## Build

Source is organized into `Core`, `Application`, `Features`, `Infrastructure`, `Presentation`, and `Bootstrap`. See [the architecture guide](docs/ARCHITECTURE.md) for dependency rules, compatibility constraints, and how to add features, persistence, and developer commands.

The project references assemblies from your Last Epoch install. If the game is not in the default Steam folder:

```powershell
dotnet build .\LastEpoch_Hud\LastEpoch_Hud.csproj -c Release -p:LastEpochPath="D:\SteamLibrary\steamapps\common\Last Epoch"
```
