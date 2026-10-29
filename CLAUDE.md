# Path to Dragon (P2D) — notes for Claude

Portrait-only mobile dragon-raising RPG in **Unity 6 (6000.3.27f1)**. The player raises one dragon,
steers its growth through upgradable equipment (Growth Paths), swaps "riders" who buff it, and farms a
weekly dungeon. The user writes in Thai — reply in Thai.

## Read first

- `docs/GDD.md` — game design (dragon, equipment & Growth Paths, riders & stars, weekly spire, Hunting Grounds, combat, economy)
- `docs/Balance.md` — every formula and number; §10 skills/traits, §11 balance simulator
- `docs/balance-report.md` — latest simulation results and the tuning history
- `docs/UI-Portrait.md`, `docs/Unity-Tech.md` — UI layout and architecture
- Interactive HTML mockup of all screens (owner-only link): https://claude.ai/artifact/PPs1o1DCEK4ardWvmEav6J

## Code layout (`Assets/_Project`)

| Assembly | Folder | Rule |
|---|---|---|
| `P2D.Domain` | `Scripts/Domain` | Pure C#, `noEngineReferences: true`. All game rules live here so the server (UGS Cloud Code) can share them. |
| `P2D.Data` | `Scripts/Data` | ScriptableObjects (`RiderDefinition`). |
| `P2D.Services` | `Scripts/Services` | Unity glue: `SaveManager`, `FileSaveStorage`, `JsonUtilitySaveSerializer`, `ServerClock`. |
| `P2D.Presentation` | `Scripts/Presentation` | `SafeAreaFitter`, `PortraitCanvasScaler`, `BattleRunner`. |
| `P2D.Editor` | `Scripts/Editor` | Portrait player-settings hook, **P2D → Balance Simulator** window. |
| `P2D.Domain.Tests` | `Tests/EditMode` | NUnit EditMode tests (186). |
| `P2D.PlayMode.Tests` | `Tests/PlayMode` | PlayMode tests for the greybox scenes (30). |

Key Domain pieces: `DragonRules`, `EquipmentRules`, `AffinityRules`, `RiderRules`, `RiderCatalog`,
`SkillCatalog`, `PathTraitRules`/`SkillLoadoutRules`, `LoadoutCalculator`, `BattleSimulation`
(deterministic, 30 ticks/s, seed + input log replays exactly), `SpireRules`/`WeeklyCalendar`,
`HuntRules`, `SummonRules`, `PlayerSave`/`SaveService`/`OfflineProgress`, `BalanceSimulator`,
`GameActions` (every menu action: hatch, equip, enhance, reforge, refine, salvage, feed, evolve, ride, star up, summon, spire battle/result),
`EquipmentGenerator`, `SetCatalog` (2/4-piece sets), `SpireThemes`, `SpireActions` (sweep, boss claims, weekly chest, catch-up rewards), `PresetActions` (3 loadout presets), `FreeSummon` (one free pull a day), `FeaturedRiders` (weekly +20% spire materials), `SpireAffixes` (weekly affixes, **off**: `Live = false` until the numbers are signed off), `HuntActions` (zones, runs, auto hunt), `ShopActions` (shard shop, stamina refill), `NestActions` (idle income, nest level), `StoryActions` (3 chapters × 10 stages, first-clear rewards), `StoryScript` (scenes before key stages), `MissionActions` (daily / weekly missions; actions without a clock record into `pending`, `Sync` folds it into today), `FeatureUnlocks` (story stage gates for missions / enhance / riders / hunt / spire / workshop; Domain refuses locked actions, test helpers set `story.cleared = StoryActions.TotalStages`).

## Conventions

- Numbers in code and in `docs/Balance.md` must match. When you change a number, update the doc and the
  tests in the same commit.
- After changing skills, riders, gear, sets or enemy stats, run the balance simulator (Unity menu
  **P2D → Balance Simulator**, or batch mode `-executeMethod P2D.Editor.BalanceSimulatorCli.Run -balanceOut <f>.md
  -balanceRuns 100 -balanceDodge 0.8` (add `-balanceAffixes ThemeSurge,Rush,Mist` to test affixes); ≥100 runs per floor) and update `docs/balance-report.md`.
- Battle code must stay deterministic: every random roll goes through the battle's `IRandom`; never use
  `UnityEngine.Random`, `System.Random`, time, or `string.GetHashCode()` inside rules.
- Save data uses only fields, arrays and `List<T>` (JsonUtility); bump `SaveMigrator.CurrentVersion` and add a
  migration step when the format changes.
- Rate stats (CRIT Rate, CRIT DMG, Element DMG, Resist) add points; other % stats multiply (`StatSheet`).
- Server time only for resets (`ServerClock`), never the device clock.
- Work happens on branch `claude/path-to-dragon-game-design-wcl2n7` (the repo has no `main` yet).

## Current state

- Project opens in **Unity 6000.3.27f1** (`ProjectSettings/ProjectVersion.txt`); `.meta` files and
  ProjectSettings are committed. EditMode and PlayMode tests pass in the real editor with no compiler warnings.
- Run tests from the command line (editor must be closed):
  `& "C:\Program Files\Unity\Hub\Editor\6000.3.27f1\Editor\Unity.exe" -batchmode -projectPath C:\code\p2d -runTests -testPlatform EditMode -testResults <out>.xml -logFile <out>.log`
- Commit the `.meta` file of every new asset or folder together with the asset.
- Art style is decided: **3D low-poly**, tilted top-down camera.
- Playable greybox, two scenes that each hold one bootstrap and build everything else in code
  (regenerate with **P2D → Create Greybox Scenes**):
  - `Scenes/Main.unity` (first in Build Settings) — `MainMenu`: loads the save via `SaveManager`, gives the
    starter kit, portrait shell + tabs DRAGON / RIDERS (→ Summon) / NEST / ADVENTURE (Story | Spire | Hunt) / SHOP (`Presentation/Screens`, built
    with `UiKit`). Every button calls `GameActions` (Domain) on the live `PlayerSave`, then `MainMenu.Commit`.
  - `Scenes/GreyboxBattle.unity` — `GreyboxBattleBootstrap` + `GreyboxBattleView` (3D primitives + HUD) driving
    `BattleSimulation` via `BattleRunner`. Opened directly it is a sandbox (BUILD cycles sample builds);
    started from the Spire tab it plays `BattleHandoff.Pending` and reports back (rewards applied, Main reloads).
  - PlayMode tests use `SaveManager.StorageOverride = new MemorySaveStorage()` so they never touch the real save.
  - Screenshots for visual review: run the explicit PlayMode test `P2D.PlayMode.Tests.ScreenshotCapture`
    (`-testFilter`); PNGs go to `Logs/Screenshots` or `P2D_SCREENSHOT_DIR`. Look at them after UI changes.
- UI text is translated: every player-facing string goes through `Loc.T` / `Loc.F` (Domain, English source text as the key, Thai in `ThaiText`; add the Thai line in the same commit — `ThaiCoverageTests` fails on English left on screen). Language is a device setting (`LanguageSetting`, default Thai, switch on the Nest screen). The UI renders Thai: `UiKit.Font` loads Noto Sans Thai (Thai + Latin, OFL)
  from `Art/Fonts/Resources`, falling back to `LegacyRuntime.ttf`. Legacy `Text` has no OpenType shaping, so every
  label gets `ThaiMarkPositioner`, which moves stacked tone marks and lower vowels into place (`docs/Unity-Tech.md` §8).
  Build new text through `UiKit.Label` (or add that component) so Thai stays readable.
- A new game opens on `HatchScreen` (tap the egg, name the dragon; bars hidden until `dragon.hatched`). Save format v2 added `hatched`; the v1 → v2 step marks older saves as hatched.
- Starter kit (`GameActions.EnsureStarterKit`) sets the dragon to Lv 10 and gives 3 scales as a stand-in for
  story chapters 1–2 (a Lv 1 dragon loses spire floor 1).
- Not built yet: real art/prefabs, IAP (packs, Dragon Pass), backend (UGS), cloud save, URP (the greybox uses the built-in pipeline).

## Open decisions (ask the user)

- Balance watch items: Arcane still clears fastest; Bastion boss fights take 80–107 s of a 120 s limit;
  Magma 4-piece (40% burst, 1 s cooldown) still leads the set builds on Tempest (75% auto boss wins vs Storm 54%); see balance-report.md.
- Weekly spire affixes are built but off (`SpireAffixes.Live`). Round 7 (theme ATK +10%, Thick hide DEF +15%, Rush 75 s) fixed Bastion; Veil of mist (crit −10) still hits Fury hard (B2 boss 46% → 3–7%). Soften Mist and switch on? (balance-report.md "Affix")
- PvP or not; always-online or partly offline.
