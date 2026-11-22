# Mod architecture

The mod remains one MelonLoader assembly. Its source is organized by responsibility, with gameplay features grouped by domain. The project uses SDK source globs; adding a normal `.cs` file does not require editing the project file.

## Source map

```text
LastEpoch_Hud/
  Bootstrap/                  MelonLoader adapter and explicit composition
    Composition/              Runtime modules and developer command providers
    Workers/                  Unity worker registration
  Core/                       Engine-independent rules, models, and contracts
    Combat/                   Combat calculations and Heorot rules
    Crafting/                 Orb, recombination, and memory rules
    Equipment/                Equipment validation, Jewels, and custom item models
    Persistence/              Repository contract
    Runtime/                  Small lifecycle contracts
    Testing/                  Developer command contract
    World/                    World rules
  Application/
    Runtime/                  Ordered lifecycle dispatcher and callback adapters
    Testing/                  Composed developer command catalog
  Features/                   Native gameplay integrations, grouped by domain
    Combat/Heorot/
    Crafting/                 Orb logic, affix state, native Forge integration
    Economy/                  Bazaar and stash
    Equipment/                Charms, Jewels, custom items, and requirements
    Loot/                     Generation, drops, pickup, and auto-loot
    World/                    Monoliths, dungeons, monsters, travel, and camera
  Infrastructure/
    Compatibility/            Engine/game compatibility patches
    Diagnostics/              Inspector and diagnostic exports
    Game/                     Scene, reference, catalog, and native UI adapters
    Input/                    Keyboard integration
    Localization/             Game-language and locale adapters
    Logging/
    Persistence/              Atomic JSON storage and save adapters
      Configuration/          Save_Manager lifecycle, DTOs, and defaults
  Presentation/
    Crafting/                 Advanced Forge, Memory Reflector, Recombinator
      Shared/                 ItemIconSource, CraftingWidgets, affix cards
    Hud/                      Menu shell, settings, pages, Force Drop, host
    Overlays/                 In-game visual overlays
  Assets/                     Embedded art, animation atlases, shaders
  LastEpoch_Hud/              Packaged runtime Assets and Locales
```

`LastEpoch_Hud.Tests` is the regression suite. `Launcher` is a separate application. `AssetBundleExport` is the Unity asset project; generated Unity project files and serialized assets are not runtime architecture layers. `Build` contains generated outputs and local backups.

## Dependencies and compatibility

Core has no Unity, IL2CPP, Harmony, or MelonLoader dependencies. Application dispatches through Core contracts. Infrastructure implements storage and engine adapters. Features own gameplay integrations; Presentation owns views and their resources. Bootstrap is the composition root that connects concrete implementations.

This is a single-assembly modular design, not separate feature DLLs. Existing native feature code still contains static integration points and compatibility facades. New orchestration should use the contracts and composition root rather than add another global singleton or mix concrete features into the entry point.

Physical paths are the source of organizational ownership. Existing `LastEpoch_Hud.Scripts.*` namespaces and root compatibility types are intentionally retained. Renaming those identities independently can break Harmony patch discovery, IL2CPP component registration, external references, or persisted data. In particular, preserve:

- `Main`, `Base`, registered MonoBehaviour names, constructors, and Unity callback names.
- Existing configuration fields, JSON keys, save paths, and stash/character scope rules.
- Equipment base IDs 42 (experiment), 43 (Charm), and 44 (Jewels), and affix IDs.
- Embedded resource names, prefab paths, and native object names.

## Lifecycle and composition

`Bootstrap/Main.cs` adapts MelonLoader callbacks to `ModComposition.Runtime`. It owns startup logging, not feature initialization logic.

`ModRuntime` snapshots explicitly ordered registrations. Modules implement only the phases they need: initialize, tick, draw, scene loaded, scene unloaded, or shutdown. `ModuleActions` adapts existing static entry points without forcing every feature to implement empty methods. Initialization and shutdown run once; recurring calls remain retryable. A failed module does not prevent subsequent modules from running, and repeated faults are throttled per module and phase.

To add a runtime feature, implement a narrow lifecycle contract or create a `ModuleActions` adapter, then register it in `ModComposition.Create`. Order prerequisites explicitly there. Do not add feature calls to `Main`. Catalog registration precedes accessory consumers; shutdown retains inspector-stop, Bazaar-save, and cache-cleanup order.

Unity MonoBehaviour workers are registered in `Bootstrap/Workers/Mods_Manager.cs`. Its typed worker factory keeps IL2CPP generic component calls visible at compile time. Keep object names, component grouping, initial activation, and save-controlled enable predicates when changing registrations.

## Equipment catalog registration

Features define their own native categories and classification policies. `EquipmentCatalogRegistry` owns the shared append operation; `CatalogAppendRules` rejects native enum overlap, occupied IDs, out-of-order registration, and IDs that cannot fit the serialized byte. Validation runs before replacing the catalog array.

Composition orders the experiment, Charm, and Jewel registration. Features no longer initialize another equipment feature as an implicit side effect. Waiting for a prerequisite catalog is retryable. Never shift a saved base ID to make a conflicting registration succeed or write native constants to extend a catalog.

## Persistence

`IJsonRepository<T>` is the storage contract. `AtomicJsonRepository<T>` serializes and flushes a complete document to an exclusively created sibling staging file before replacing the destination. `JsonStorePolicy` makes formatting and backup behavior explicit. Per-store locks protect their state transitions; callers publish new in-memory state after a successful commit where their transaction requires it.

Missing files and corrupt files are different outcomes. `TryLoad` returns false for a missing file; invalid JSON, null documents, validation failures, and I/O errors propagate. Callers decide recovery. Backups are never loaded silently. An existing `.tmp` left by an interrupted process is preserved for explicit inspection rather than overwritten. Inspect and recover or move that file before retrying a blocked write.

`Save_Manager` remains the Unity-facing compatibility adapter. Its lifecycle, nested data types, and defaults live in separate partial files; JSON layout and registered identity remain unchanged. Attunement, stash materials, equipped accessories, Jewel LP, and memory metadata reuse the repository while retaining their own validation and transaction rules.

## Crafting presentation

Each window has a small lifecycle coordinator and named partial files for layout, interactions, slots, tooltips, affix cards, and distinct VFX responsibilities. These partials share the original runtime type; they do not introduce new Unity component identities. The split makes responsibilities navigable while retaining existing callback/state behavior.

`ItemIconSource` owns the asynchronous sprite load and hidden native item widget used for rarity backgrounds. A window creates it when an item changes, polls it while needed, and disposes it when replaced or closed. Keep resource ownership with the window; do not share a disposable source between windows. `CraftingWidgets` supplies shared native-widget and rectangle operations.

Gameplay selection, protected-affix rules, and crafting transactions remain in Features/Core, outside the presentation helpers. New shared visuals should extend a focused helper instead of copying another window's loader or embedding gameplay mutations into widget construction.

The Force Drop builder also separates layout, widgets, selection rules, item search, affix choices, picker rendering, and item creation. Damage meter assets, view, event hooks, and recording have separate files. Quad stash capacity, rendering, storage, and native hooks have separate files. These remain compatibility types with named partials; extracting a section into a new runtime type later requires checking its native and public callers first.

## Developer commands

`ITestActionProvider` lets each feature expose commands as IDs, labels, and callbacks. `TestActionCatalog` snapshots ordered providers and rejects duplicate IDs without executing commands. `TestActionFactory` composes equipment and orb providers; Bootstrap configures `ItemsTestDropsPage` before the HUD builds. The page renders commands without referencing concrete gameplay features.

Add commands to the owning provider, or add a new provider to the factory. Orb test commands are derived from the material catalog, so a new material does not need duplicate menu wiring.

## Validation

Build against the target game's generated IL2CPP assemblies, then run the full suite against that exact DLL:

```powershell
$gamePath = 'D:\SteamLibrary\steamapps\common\Last Epoch'
dotnet build .\LastEpoch_Hud\LastEpoch_Hud.csproj -c Keyboard -p:LastEpochPath="$gamePath"
$env:LAST_EPOCH_PATH = $gamePath
dotnet test .\LastEpoch_Hud.Tests\LastEpoch_Hud.Tests.csproj
```

Tests compile Core, Application runtime/commands, and persistence independently of Unity. Cecil architecture checks enforce Core's engine independence and keep concrete feature calls out of Main. Runtime tests cover ordering, phase separation, fault isolation, throttling, and terminal shutdown. Persistence tests cover malformed data, atomic commits, backups, staged-file conflicts, and rollback behavior. Existing native patch, equipment, affix, locale, and crafting regressions remain in the suite.

Compilation and automated tests do not replace live gameplay verification. After installing, check menu startup, all three crafting windows, accessory loading/stat application, orb transactions, Bazaar, and saves across a restart. Installation and game launch are separate actions from source refactoring.
