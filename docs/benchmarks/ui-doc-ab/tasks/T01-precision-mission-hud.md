# T01: Precision Mission HUD

Build the requested **uGUI** screen in the currently open blank Unity scene.

Do not create gameplay scripts. Do not edit Scene YAML directly. Keep all visible UI under one root Canvas.

Use these exact GameObject names so the benchmark can inspect the result:

- `BenchmarkCanvas`
- `Background`
- `Title`
- `Subtitle`
- `StatusPanel`
- `PowerLabel`
- `BarBackground`
- `BarFill`
- `DeployButton`

## Canvas

- target Game View: `1280x720`
- root Canvas name: `BenchmarkCanvas`
- Screen Space Overlay
- CanvasScaler: Scale With Screen Size
- reference resolution: `1280x720`
- match width/height: `0.5`
- there must be exactly one root Canvas when you finish

## Visual layout

`Background`

- full-stretch under the Canvas
- color `#101522FF`

`Title`

- legacy uGUI Text
- text: `MISSION CONTROL`
- top-center anchor and pivot
- anchored position: `(0,-56)`
- size: `(600,72)`
- font size: `36`
- centered text
- color `#F4F1E8FF`

`Subtitle`

- legacy uGUI Text
- text: `SYSTEM READY`
- top-center anchor and pivot
- anchored position: `(0,-118)`
- size: `(500,48)`
- font size: `22`
- centered text
- color `#6FD6FFFF`

`StatusPanel`

- centered
- anchored position: `(0,-10)`
- size: `(560,220)`
- Image color `#1B263BFF`

`PowerLabel`, child of `StatusPanel`

- legacy uGUI Text
- text: `POWER  73%`
- centered
- anchored position: `(0,42)`
- size: `(420,48)`
- font size: `30`
- color `#F4F1E8FF`

`BarBackground`, child of `StatusPanel`

- centered
- anchored position: `(0,-18)`
- size: `(400,24)`
- Image color `#2A344AFF`

`BarFill`, child of `BarBackground`

- left-center anchor/pivot
- anchored position: `(0,0)`
- size: `(292,24)`
- Image color `#4DD4ACFF`
- it should visually fill exactly 73% of the 400 px bar

`DeployButton`

- uGUI Button with a visible Image
- centered horizontally
- anchored position: `(0,-178)`
- size: `(240,64)`
- button Image color `#245A8DFF`
- visible label text: `DEPLOY`
- label font size: `24`
- label color: `#FFFFFFFF`

## Interaction and final verification

- ensure a working EventSystem exists
- `DeployButton` must be reachable as the top uGUI raycast target at its center
- final Unity Console error count must be zero
- verify the final visible result before finishing

Report only the final evidence and any remaining uncertainty.