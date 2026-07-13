# build-uitk-schema

Extracts the **built-in UI Toolkit surface** (UXML elements + their attributes,
plus supported USS properties) from each installed Unity Editor by reflection,
and compresses it into the per-version bundle the connector ships.

Why reflection (not a doc scrape or a package version): UI Toolkit runtime is the
built-in `UnityEngine.UIElementsModule` engine module, so — unlike
`com.unity.ugui` — there is no `package.json` version to bucket on. The precise
per-version element/attribute/USS surface lives only in the module assemblies.
This mirrors `build-unity-docs`, but the source is live in-editor reflection
instead of offline HTML. See `docs/UITK_VERSION_RULES.md` for the design.

## Two steps

The reflection must run **inside** each Editor (the connector already lives
there), then a Go step validates and gzips the result.

```bash
# 1) In each installed Editor (open the project, ensure `hera-agent-unity status`
#    reports ready), dump the schema for that version:
hera-agent-unity exec --file tools/build-uitk-schema/dump_uitk_schema.cs
#    -> writes <editor temp>/uitk_schema_<bucket>.jsonl and returns its path

# 2) Validate + gzip it into the shipped bundle (bucket taken from the meta line):
go run ./tools/build-uitk-schema --in "<that path>"
#    -> AgentConnector/Editor/Data/uitk_schema_<bucket>.jsonl.gz.bytes
```

Repeat step 1–2 in every target Editor: `2022.3`, `2023.2`, `6000.0`, `6000.3`,
`6000.5` (same bucket set as the docs bundles). Commit each
`uitk_schema_<bucket>.jsonl.gz.bytes` **and its `.meta`** (the `Data/` folder is
an immutable UPM folder — a bundle without a sibling `.meta` is ignored). Clone an
existing `Data/*.bytes.meta`, then issue a fresh GUID.

## What the dump captures

`dump_uitk_schema.cs` branches on the Unity element architecture (detected by
`VisualElementFactoryRegistry.RegisterEngineFactories` presence):

- **2022.3 .. 6000.3:** reads `VisualElementFactoryRegistry.factories` (the tag →
  factory map Unity uses to resolve UXML); each factory carries its attributes.
- **6000.5+:** `RegisterEngineFactories` is gone and the registry is only lazily/
  partially populated, so it enumerates module `VisualElement` types that still
  carry a nested `UxmlFactory` (deterministic, same set) and reads attributes
  from `UxmlDescriptionRegistry` keyed by each element's `UxmlSerializedData`.

USS comes from `StylePropertyUtil.s_IdToName` on every version. Per line (see
`AgentConnector/Editor/Core/UiToolkitStore.cs`, loaded like `UnityDocsStore`):

```jsonc
{"kind":"meta","unity_version":"2022.3.62f2","bucket":"2022.3","uxml_element_attribute":false,"uxml_traits":"present","elements":48,"structural":4,"uss_properties":92}
{"kind":"element","element":"Button","full_type":"UnityEngine.UIElements.Button","surface":"runtime","attributes":[{"name":"text","type":"string","default":""}]}
{"kind":"structural","element":"UXML","full_type":"UnityEngine.UIElements.VisualElement","surface":"runtime","attributes":[...]}
{"kind":"uss","name":"flex-direction","animatable":true}
```

Design decisions baked into the dump (all reflection-derived, no inference):

- **Runtime elements only.** The dump keeps only elements from
  `UnityEngine.UIElementsModule` — the built-in runtime element library. The
  factory registry also holds editor controls (`UnityEditor.CoreModule`, mixed
  with version-variable editor internals + UI Builder), package elements (Shader
  Graph, Tilemap, GraphView) and project custom controls; all are excluded, so
  the bundle is deterministic across projects and Unity versions and matches the
  emitter's runtime-UI scope. Editor UI generation, if added later, needs a
  curated set — assembly alone is too noisy (2022.3's `CoreModule` alone has ~57
  factory entries vs 6000.3's 22).
- **Surface** is recorded from the element namespace (`UnityEngine.UIElements.*`
  = runtime); with the runtime-only filter it is always `runtime`. The field
  stays for schema stability.
- **USS** enumerates `StylePropertyUtil.s_IdToName` (id → canonical kebab name),
  the version-robust source — the newer `ussNameToCSharpName` property is absent
  in 2022.3.
- **`kind:"structural"`** marks UXML document directives (`UXML`, `Template`,
  `Style`, `AttributeOverrides`) — a factory whose created type is exactly
  `VisualElement` but whose tag is not `VisualElement`. Kept for completeness,
  excluded from the element allow-list.
- **`uxml_traits`** (`obsolete`/`present`/`absent`) and `uxml_element_attribute`
  record the authoring-API generation for that version.
- Attribute `type` is the `UxmlXAttributeDescription` class stripped to `x`
  (`string`, `bool`, `int`, `enum`, `float`, `asset`, `type`, `image`, …).
- USS `animatable` comes from `StylePropertyUtil.IsAnimatable` — it is the
  substrate for system-aware Game Feel juice under `ui_system: uitk`.
