# Path to Dragon (P2D) — notes for Claude

Portrait-only mobile dragon-raising RPG in **Unity 6 LTS (6000.0.x)**. The player raises one dragon,
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
| `P2D.Domain.Tests` | `Tests/EditMode` | NUnit EditMode tests (98, all passing at handoff). |

Key Domain pieces: `DragonRules`, `EquipmentRules`, `AffinityRules`, `RiderRules`, `RiderCatalog`,
`SkillCatalog`, `PathTraitRules`/`SkillLoadoutRules`, `LoadoutCalculator`, `BattleSimulation`
(deterministic, 30 ticks/s, seed + input log replays exactly), `SpireRules`/`WeeklyCalendar`,
`HuntRules`, `SummonRules`, `PlayerSave`/`SaveService`/`OfflineProgress`, `BalanceSimulator`.

## Conventions

- Numbers in code and in `docs/Balance.md` must match. When you change a number, update the doc and the
  tests in the same commit.
- After changing skills, riders, gear or enemy stats, run the balance simulator (Unity menu
  **P2D → Balance Simulator**, ≥100 runs per floor for boss decisions) and update `docs/balance-report.md`.
- Battle code must stay deterministic: every random roll goes through the battle's `IRandom`; never use
  `UnityEngine.Random`, `System.Random`, time, or `string.GetHashCode()` inside rules.
- Save data uses only fields, arrays and `List<T>` (JsonUtility); bump `SaveMigrator.CurrentVersion` and add a
  migration step when the format changes.
- Rate stats (CRIT Rate, CRIT DMG, Element DMG, Resist) add points; other % stats multiply (`StatSheet`).
- Server time only for resets (`ServerClock`), never the device clock.
- Work happens on branch `claude/path-to-dragon-game-design-wcl2n7` (the repo has no `main` yet).

## State at handoff (from the cloud session)

- Unity-dependent scripts were only compile-checked against API stubs, never opened in the real editor.
  First local task: open the project, let Unity generate `ProjectSettings` and `.meta` files, fix any
  compile errors, run all EditMode tests, then commit the generated `.meta` files and `ProjectSettings`.
- `Packages/manifest.json` pins guessed Unity 6 package versions; accept Unity's suggested versions if it complains.
- Not built yet: scenes, prefabs, real UI screens, battle view, backend (UGS), cloud save, nest income
  numbers, equipment set bonuses, sub-stat rolling, path visuals.

## Open decisions (ask the user)

- Art style: 3D low-poly (recommended) vs 2D skeletal — needed before scenes/battle view.
- Balance watch items: Arcane still clears fastest; Bastion boss fights take 80–107 s of a 120 s limit.
- PvP or not; always-online or partly offline.
