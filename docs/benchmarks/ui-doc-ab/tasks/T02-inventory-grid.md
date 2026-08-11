# T02: Inventory Grid

Build the requested **uGUI** inventory screen in the currently open blank Unity scene.

Do not create gameplay scripts. Do not edit Scene YAML directly. Keep all visible UI under one root Canvas.

Use these exact GameObject names so the benchmark can inspect the result:

- `BenchmarkCanvas`
- `Background`
- `InventoryPanel`
- `InventoryTitle`
- `GridRoot`
- `Slot_01` through `Slot_12`
- `CloseButton`

Each slot must contain one visible legacy uGUI Text child named `Label` whose text is the two-digit slot number, `01` through `12`.

## Canvas

- target Game View: `1280x720`
- root Canvas: `BenchmarkCanvas`
- Screen Space Overlay
- CanvasScaler: Scale With Screen Size
- reference resolution: `1280x720`
- match width/height: `0.5`
- exactly one root Canvas at finish

## Layout

`Background`

- full stretch
- color `#0E1320FF`

`InventoryPanel`

- centered
- size `(760,500)`
- Image color `#1A2435FF`

`InventoryTitle`, child of `InventoryPanel`

- legacy uGUI Text
- text `INVENTORY`
- anchored position `(0,205)`
- size `(420,56)`
- font size `32`
- centered
- color `#F2F5F8FF`

`GridRoot`, child of `InventoryPanel`

- centered
- anchored position `(0,-20)`
- size `(528,292)`

Create exactly 12 visible slot Images under `GridRoot`, arranged as **4 columns x 3 rows**.

Every slot:

- size `(120,88)`
- Image color `#26364DFF`
- contains one centered `Label`
- Label font size `20`
- Label color `#D9E2ECFF`

Use these slot centers relative to `GridRoot`:

```text
row 1: (-204, 102)  (-68, 102)  (68, 102)  (204, 102)
row 2: (-204,   0)  (-68,   0)  (68,   0)  (204,   0)
row 3: (-204,-102)  (-68,-102)  (68,-102)  (204,-102)
```

This corresponds to 16 px horizontal gaps and 14 px vertical gaps between the slot rectangles.

`CloseButton`, child of `InventoryPanel`

- uGUI Button
- anchored position `(320,205)`
- size `(64,44)`
- Image color `#7A4651FF`
- visible legacy label text `X`
- label font size `24`
- label color `#FFFFFFFF`

## Interaction and final verification

- ensure a working EventSystem exists
- `CloseButton` must be reachable as the top uGUI raycast target at its center
- there must be exactly 12 slot objects with no duplicate slot names
- final Unity Console error count must be zero
- verify the final visible result before finishing

Report only the final evidence and any remaining uncertainty.