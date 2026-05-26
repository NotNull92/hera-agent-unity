using System;
using System.Collections.Generic;

namespace HeraAgent
{
    /// <summary>
    /// One pitfall entry — a short warning plus an optional Unity Manual slug
    /// (rendered into an absolute URL at lookup time).
    /// </summary>
    internal class PitfallEntry
    {
        public string Text { get; }
        public string DocSlug { get; }

        public PitfallEntry(string text, string docSlug = null)
        {
            Text = text;
            DocSlug = docSlug;
        }
    }

    /// <summary>
    /// Static catalog of Unity Editor / uGUI / MonoBehaviour pitfalls for Unity 6 (6000.0+).
    ///
    /// Connector requires Unity 6, so the catalog targets that version exclusively — no version
    /// branching. Add only entries that are stable across Unity 6.x minor versions; package-
    /// specific quirks (Entities 1.x, Addressables 2.x) belong in introspection, not here.
    /// </summary>
    internal static class UnityPitfalls
    {
        // Unity 6 (6000.0+) Korean manual.
        private const string ManualBase = "https://docs.unity3d.com/kr/current/Manual/";

        private static readonly Dictionary<string, PitfallEntry[]> s_Catalog = new(StringComparer.Ordinal)
        {
            ["UnityEditor.EditorApplication"] = new[]
            {
                new PitfallEntry(
                    "delayCall fires after the next editor update, but the editor may execute a domain reload between subscribing and firing — handlers can be lost silently. Re-subscribe in [InitializeOnLoad] or after AssemblyReloadEvents.afterAssemblyReload.",
                    "class-EditorApplication"),
                new PitfallEntry(
                    "update is called every editor tick; do not do heavy work there. Use delayCall for one-shots and EditorApplication.timeSinceStartup gating for periodic work.",
                    "class-EditorApplication"),
                new PitfallEntry(
                    "isPlaying flips before OnEnable/OnDisable on MonoBehaviours run. To react to mode transitions, subscribe to playModeStateChanged instead.",
                    "class-EditorApplication"),
            },
            ["UnityEditor.AssetDatabase"] = new[]
            {
                new PitfallEntry(
                    "Wrap batch asset edits in StartAssetEditing/StopAssetEditing — otherwise every CreateAsset/ImportAsset triggers its own refresh and the loop takes 10x+ longer.",
                    "class-AssetDatabase"),
                new PitfallEntry(
                    "Refresh() during play mode is blocked by Unity. Use 'hera-agent-unity-pro editor refresh --force' or call from edit mode.",
                    "class-AssetDatabase"),
                new PitfallEntry(
                    "SaveAssets() does not flush in-memory ScriptableObject changes that were not modified via SerializedObject. Call EditorUtility.SetDirty(obj) first.",
                    "class-AssetDatabase"),
                new PitfallEntry(
                    "GUIDs are stable across renames; paths are not. Prefer AssetDatabase.GUIDToAssetPath when persisting references.",
                    "class-AssetDatabase"),
            },
            ["UnityEditor.EditorUtility"] = new[]
            {
                new PitfallEntry(
                    "SetDirty only marks the object; the change is not written to disk until AssetDatabase.SaveAssets or a project save. Pair the two for round-trip persistence.",
                    "class-EditorUtility"),
                new PitfallEntry(
                    "DisplayDialog blocks the main thread — never call from exec code that runs inside the request queue, it will deadlock the HTTP listener.",
                    "class-EditorUtility"),
            },
            ["UnityEngine.GameObject"] = new[]
            {
                new PitfallEntry(
                    "Find / FindGameObjectsWithTag walk the entire scene each call (O(n)). Cache results — do not call inside Update.",
                    "class-GameObject"),
                new PitfallEntry(
                    "DestroyImmediate is required when removing objects from edit mode or in tests — Destroy schedules for end-of-frame and silently no-ops outside play.",
                    "class-GameObject"),
            },
            ["UnityEngine.Object"] = new[]
            {
                new PitfallEntry(
                    "== against null is overloaded — it returns true for destroyed-but-not-yet-GC'd Unity objects. Do not use ReferenceEquals(obj, null) when checking for destruction.",
                    "class-Object"),
                new PitfallEntry(
                    "Instantiate creates the new object in the active scene; for editor-only objects pass HideFlags.DontSave to avoid scene dirty marks.",
                    "class-Object"),
            },
            ["UnityEditor.SceneManagement.EditorSceneManager"] = new[]
            {
                new PitfallEntry(
                    "OpenScene with OpenSceneMode.Single discards unsaved changes without prompting. Check GetActiveScene().isDirty first or use the SaveOpenScenes API.",
                    "class-EditorSceneManager"),
                new PitfallEntry(
                    "MarkSceneDirty is required after programmatic scene mutations — without it the scene is not flagged for save and changes are lost on next open.",
                    "class-EditorSceneManager"),
            },
            ["UnityEngine.Debug"] = new[]
            {
                new PitfallEntry(
                    "In an exec-style context, prefer 'return value;' over Debug.Log — the CLI surfaces return values as JSON. Debug.Log only appears in the editor console (readable via 'hera-agent-unity-pro console').",
                    "class-Debug"),
            },
            ["UnityEditor.SerializedObject"] = new[]
            {
                new PitfallEntry(
                    "ApplyModifiedProperties() is required after any property mutation, otherwise the object is reset on the next Update() call. Forgetting this is the most common silent-no-op bug.",
                    "class-SerializedObject"),
                new PitfallEntry(
                    "Update() refreshes from the underlying object — call before reading if anything else may have mutated the target.",
                    "class-SerializedObject"),
            },
            ["UnityEditor.Compilation.CompilationPipeline"] = new[]
            {
                new PitfallEntry(
                    "RequestScriptCompilation returns immediately; compilation runs asynchronously. To wait, poll EditorApplication.isCompiling or use the heartbeat 'ready' state.",
                    "ScriptCompileOrder"),
            },
            ["UnityEngine.Application"] = new[]
            {
                new PitfallEntry(
                    "dataPath in the editor points to <Project>/Assets — not the platform-specific runtime path. Use Application.persistentDataPath for save data.",
                    "class-Application"),
                new PitfallEntry(
                    "isPlaying differs from EditorApplication.isPlaying only in standalone builds; in the editor they are equivalent. Prefer the EditorApplication variant in editor scripts for clarity.",
                    "class-Application"),
            },
            ["UnityEngine.Resources"] = new[]
            {
                new PitfallEntry(
                    "Resources.Load is synchronous and reads from the Resources folder only — not from Addressables or AssetBundles. Prefer Addressables in new Unity 6 code.",
                    "LoadingResourcesatRuntime"),
            },

            // ─── MonoBehaviour lifecycle ────────────────────────────
            ["UnityEngine.MonoBehaviour"] = new[]
            {
                new PitfallEntry(
                    "Lifecycle order is Awake → OnEnable → Start (then Update each frame). Awake runs even on inactive GameObjects if the component is enabled. Start only runs once the GameObject becomes active for the first time.",
                    "ExecutionOrder"),
                new PitfallEntry(
                    "StartCoroutine on an inactive GameObject throws immediately. Activate first or use a dedicated runner GameObject that's always active.",
                    "Coroutines"),
                new PitfallEntry(
                    "OnDestroy fires for every component whose Awake has run, including on inactive GameObjects. Do not assume the object was ever 'started'.",
                    "ExecutionOrder"),
                new PitfallEntry(
                    "OnValidate is called on the main thread when a serialized field changes in the Inspector or when the asset reloads — do not call Unity APIs that allocate or modify the scene (it can fire during deserialization before the object is fully initialized).",
                    "class-MonoBehaviour"),
                new PitfallEntry(
                    "[ExecuteAlways] runs Update in edit mode AND play mode. [ExecuteInEditMode] is legacy and edit-mode only. Use ExecuteAlways for new code and guard with Application.isPlaying.",
                    "ExecuteInEditMode"),
                new PitfallEntry(
                    "Coroutines are tied to the MonoBehaviour instance: disabling the component or destroying the GameObject silently stops them. Hold a reference and StopCoroutine if you need deterministic cancellation.",
                    "Coroutines"),
                new PitfallEntry(
                    "Unity 6 exposes MonoBehaviour.destroyCancellationToken. Pass it to async methods so they cancel automatically when the component is destroyed — prefer this over manual CancellationTokenSource bookkeeping.",
                    "class-Awaitable"),
            },
            ["UnityEngine.Awaitable"] = new[]
            {
                new PitfallEntry(
                    "Awaitable is Unity 6's main-thread-aware async primitive — Awaitable.NextFrameAsync(), Awaitable.WaitForSecondsAsync(), Awaitable.MainThreadAsync(). Composes with destroyCancellationToken for auto-cancel on destruction.",
                    "class-Awaitable"),
                new PitfallEntry(
                    "Awaitable.FromAsyncOperation wraps an AsyncOperation (e.g. SceneManager.LoadSceneAsync) into an awaitable. The recommended pattern for await-based scene/asset loading in Unity 6.",
                    "class-Awaitable"),
            },
            ["UnityEngine.WaitForSeconds"] = new[]
            {
                new PitfallEntry(
                    "new WaitForSeconds(t) allocates every call — cache the instance as a private static readonly field when the duration is constant.",
                    "Coroutines"),
                new PitfallEntry(
                    "WaitForSeconds is affected by Time.timeScale. When the game is paused (timeScale=0) it stalls forever. Use WaitForSecondsRealtime for UI/menu coroutines.",
                    "TimeFrameManagement"),
            },
            ["UnityEngine.ScriptableObject"] = new[]
            {
                new PitfallEntry(
                    "OnEnable runs after every domain reload, not just once. Initializing state from null inside OnEnable resets it on every recompile — use [SerializeField] private fields for persistent state.",
                    "class-ScriptableObject"),
                new PitfallEntry(
                    "Construct with ScriptableObject.CreateInstance<T>(), never 'new T()'. Calling new bypasses Unity's serialization machinery and produces an object that vanishes after a domain reload.",
                    "class-ScriptableObject"),
            },

            // ─── uGUI ───────────────────────────────────────────────
            ["UnityEngine.Canvas"] = new[]
            {
                new PitfallEntry(
                    "Render Mode determines which fields matter: ScreenSpaceOverlay ignores worldCamera; ScreenSpaceCamera and WorldSpace require worldCamera to be set or input/rendering breaks silently.",
                    "class-Canvas"),
                new PitfallEntry(
                    "Any change to a child Graphic forces the whole Canvas to rebatch. Static/rarely-changing UI and dynamic UI should live on separate sub-Canvases to keep dirty regions small.",
                    "UICanvas"),
                new PitfallEntry(
                    "SetActive(false) on a Canvas root triggers a full rebatch when re-enabled. For show/hide use CanvasGroup.alpha=0 + blocksRaycasts=false instead — it's effectively free.",
                    "class-CanvasGroup"),
            },
            ["UnityEngine.RectTransform"] = new[]
            {
                new PitfallEntry(
                    "anchorMin/anchorMax are parent-relative fractions; pivot is the local rotation/scale origin. They are independent — changing anchors does not move the pivot and vice versa.",
                    "class-RectTransform"),
                new PitfallEntry(
                    "sizeDelta means different things depending on anchors: with single-point anchors it's width/height in pixels; with stretched anchors it's the offset (left+right or top+bottom). Setting sizeDelta = new Vector2(100, 100) on a stretched RectTransform almost never does what you expect.",
                    "UIBasicLayout"),
                new PitfallEntry(
                    "SetParent(newParent, false) is the UI default — worldPositionStays=true (the bare overload) breaks UI coordinates because anchored positions are interpreted in the new parent's space.",
                    "class-Transform"),
            },
            ["UnityEngine.EventSystems.EventSystem"] = new[]
            {
                new PitfallEntry(
                    "If the scene has no EventSystem, every UI click/drag/hover handler is silently inactive. Standalone scenes loaded additively also need exactly one EventSystem total — duplicates are no-ops.",
                    "EventSystem"),
                new PitfallEntry(
                    "SetSelectedGameObject(null) clears the current selection (used to drop keyboard/gamepad focus). Passing the same object twice does nothing — it is *not* a refresh.",
                    "EventSystem"),
            },
            ["UnityEngine.UI.GraphicRaycaster"] = new[]
            {
                new PitfallEntry(
                    "A Canvas without a GraphicRaycaster cannot receive UI input — even Buttons under it become inert. Required component, not optional.",
                    "script-GraphicRaycaster"),
                new PitfallEntry(
                    "blockingObjects/blockingMask control whether 3D/2D world objects between the camera and the UI swallow the click. The default 'None' means UI always wins; switch to All if a 3D collider needs to block.",
                    "script-GraphicRaycaster"),
            },
            ["UnityEngine.UI.LayoutGroup"] = new[]
            {
                new PitfallEntry(
                    "Direct width/height changes on a child get overwritten on the next layout pass. To 'pin' a size, either use LayoutElement.preferredWidth/Height or disable the controlling axis on the LayoutGroup.",
                    "UIAutoLayout"),
                new PitfallEntry(
                    "Nested LayoutGroups multiply the rebuild cost — a dirty grandchild propagates up through every parent. Prefer one or two layers; for deep structures use LayoutRebuilder.MarkLayoutForRebuild + a manual pass.",
                    "UIAutoLayout"),
            },
            ["UnityEngine.UI.ContentSizeFitter"] = new[]
            {
                new PitfallEntry(
                    "ContentSizeFitter sets the RectTransform's sizeDelta from the layout's preferred size — once. If you then assign sizeDelta manually it is overwritten on the next layout pass. Disable the fitter or let it own the size.",
                    "script-ContentSizeFitter"),
                new PitfallEntry(
                    "Combining ContentSizeFitter with a stretched anchor produces undefined sizing — the fitter writes sizeDelta as a delta to the stretched rect, not as an absolute size. Use single-point anchors (e.g. top-left) when fitting content.",
                    "script-ContentSizeFitter"),
            },
            ["UnityEngine.UI.ScrollRect"] = new[]
            {
                new PitfallEntry(
                    "Viewport requires a Mask (or RectMask2D) component or content shows outside the visible area. RectMask2D is rectangular-only but cheaper than Mask (no stencil buffer).",
                    "script-ScrollRect"),
                new PitfallEntry(
                    "Setting velocity = Vector2.zero mid-drag suppresses OnEndDrag callbacks for plugins that rely on it. Prefer StopMovement() when you want to halt programmatically.",
                    "script-ScrollRect"),
                new PitfallEntry(
                    "inertia + decelerationRate control momentum after release. Setting inertia=false makes the scroll snap-stop on release, which can feel broken on touch — only do it intentionally.",
                    "script-ScrollRect"),
            },
            ["UnityEngine.UI.Selectable"] = new[]
            {
                new PitfallEntry(
                    "interactable=false blocks OnPointerClick but NOT OnPointerEnter/OnPointerExit. Hover effects can still fire on a disabled button.",
                    "script-Selectable"),
                new PitfallEntry(
                    "Navigation.mode = Automatic auto-selects neighbors based on layout, which can move keyboard/gamepad focus into elements you didn't intend (e.g. inside scroll views). Set Explicit or None when in doubt.",
                    "script-Selectable"),
            },
            ["UnityEngine.UI.Mask"] = new[]
            {
                new PitfallEntry(
                    "Mask uses the stencil buffer (extra draw calls, breaks dynamic batching for masked children). Use RectMask2D for rectangular clipping where possible — it's free in comparison.",
                    "script-Mask"),
            },
            ["UnityEngine.CanvasGroup"] = new[]
            {
                new PitfallEntry(
                    "alpha cascades to all child Graphics. interactable=false also disables children. blocksRaycasts=false makes the group click-through (pointer hits whatever is behind it).",
                    "class-CanvasGroup"),
                new PitfallEntry(
                    "Disabling interactable on a parent CanvasGroup is the cleanest way to gate a whole panel without iterating over every Selectable.",
                    "class-CanvasGroup"),
            },
        };

        /// <summary>Returns pitfalls for the type, or an empty list if none are catalogued.</summary>
        internal static IReadOnlyList<RenderedPitfall> Lookup(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName)) return Array.Empty<RenderedPitfall>();
            if (!s_Catalog.TryGetValue(fullTypeName, out var entries)) return Array.Empty<RenderedPitfall>();

            var rendered = new List<RenderedPitfall>(entries.Length);
            foreach (var e in entries) rendered.Add(Render(e));
            return rendered;
        }

        private static RenderedPitfall Render(PitfallEntry e)
        {
            var docUrl = string.IsNullOrEmpty(e.DocSlug) ? null : ManualBase + e.DocSlug + ".html";
            return new RenderedPitfall(e.Text, docUrl);
        }
    }

    /// <summary>Pitfall after rendering — what describe_type returns.</summary>
    internal class RenderedPitfall
    {
        public string Text { get; }
        public string DocUrl { get; }

        public RenderedPitfall(string text, string docUrl)
        {
            Text = text;
            DocUrl = docUrl;
        }
    }
}
