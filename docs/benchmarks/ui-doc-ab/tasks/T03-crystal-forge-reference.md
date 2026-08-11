# T03: Crystal Forge Static Reference Recreation

Recreate the attached **Crystal Forge** reference as a static **uGUI** screen in the currently open blank Unity scene.

Reference asset used by the benchmark runner:

`docs/benchmarks/user-scenario/assets/crystal-forge-win-6000.3.5f2.png`

Reference SHA-256:

`1383b9d1175c4777ee866be24617287e5728fc5fec92503cf4c19fa78f5742f7`

Do not create gameplay logic or scripts. The buttons only need correct visible styling and working uGUI raycast targets. Do not edit Scene YAML directly.

Use these exact GameObject names so the benchmark can inspect the result without telling you the target geometry:

- `BenchmarkCanvas`
- `OuterBackground`
- `RootPanel`
- `Title`
- `Subtitle`
- `CrystalCount`
- `PowerLabel`
- `MineButton`
- `UpgradeButton`
- `RestartButton`
- `CompleteLabel`

## Required visible text

Match the reference image, including capitalization and spacing as closely as Unity Text permits:

- `CRYSTAL FORGE`
- `Power the beacon before the night ends`
- `CRYSTALS  6 / 6`
- `MINING POWER  x2`
- `◆  MINE CRYSTAL`
- `UPGRADE  ·  COST 3`
- `RESTART`
- `Forge Complete!`

## Rendering constraints

- target Game View: `1280x720`
- use one Screen Space Overlay Canvas named `BenchmarkCanvas`
- use CanvasScaler, Scale With Screen Size, reference `1280x720`, match `0.5`
- use legacy uGUI Text for deterministic benchmark rendering
- use plain uGUI Images/Buttons; no imported artwork is required
- match the reference's panel size, spacing, button proportions, alignment, and major colors
- do not add extra visible decoration that is not present in the reference

## Interaction and final verification

- ensure a working EventSystem exists
- `MineButton`, `UpgradeButton`, and `RestartButton` must each be reachable as the top uGUI raycast target at their visual centers
- all required text must be visibly rendered, not merely stored in a component
- final Unity Console error count must be zero
- compare the final visible screen to the reference before finishing

Report only the final evidence and any remaining uncertainty.