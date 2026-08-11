string PathOf(UnityEngine.Transform t)
{
    if (t == null) return null;
    var parts = new System.Collections.Generic.List<string>();
    var current = t;
    while (current != null)
    {
        parts.Add(current.name);
        current = current.parent;
    }
    parts.Reverse();
    return "/" + string.Join("/", parts);
}

object V2(UnityEngine.Vector2 v) => new { x = v.x, y = v.y };
object Color(UnityEngine.Color c) => new { r = c.r, g = c.g, b = c.b, a = c.a };

var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var nodes = new System.Collections.Generic.List<object>();
var buttons = new System.Collections.Generic.List<UnityEngine.UI.Button>();
var rootCanvasCount = 0;
var rootNames = new System.Collections.Generic.List<string>();
foreach (var root in scene.GetRootGameObjects()) rootNames.Add(root.name);

var allObjects = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>();
foreach (var go in allObjects)
{
    if (!go.scene.IsValid() || go.scene.handle != scene.handle) continue;
    if (UnityEditor.EditorUtility.IsPersistent(go)) continue;

    var rt = go.GetComponent<UnityEngine.RectTransform>();
    var image = go.GetComponent<UnityEngine.UI.Image>();
    var text = go.GetComponent<UnityEngine.UI.Text>();
    var button = go.GetComponent<UnityEngine.UI.Button>();
    var canvas = go.GetComponent<UnityEngine.Canvas>();
    var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
    if (button != null) buttons.Add(button);
    if (canvas != null && canvas.isRootCanvas) rootCanvasCount++;

    object rect = null;
    if (rt != null)
    {
        rect = new {
            anchor_min = V2(rt.anchorMin),
            anchor_max = V2(rt.anchorMax),
            anchored_position = V2(rt.anchoredPosition),
            size_delta = V2(rt.sizeDelta),
            pivot = V2(rt.pivot),
            size = new { width = rt.rect.width, height = rt.rect.height }
        };
    }

    object imageInfo = null;
    if (image != null)
    {
        imageInfo = new {
            color = Color(image.color),
            enabled = image.enabled,
            raycast_target = image.raycastTarget,
            image_type = image.type.ToString()
        };
    }

    object textInfo = null;
    if (text != null)
    {
        var visible = text.enabled && go.activeInHierarchy && text.font != null && text.color.a > 0.01f
            && rt != null && rt.rect.width > 0.5f && rt.rect.height > 0.5f;
        textInfo = new {
            value = text.text,
            font_size = text.fontSize,
            color = Color(text.color),
            alignment = text.alignment.ToString(),
            enabled = text.enabled,
            font = text.font != null ? text.font.name : null,
            visibly_configured = visible,
            raycast_target = text.raycastTarget
        };
    }

    object buttonInfo = null;
    if (button != null)
    {
        var label = go.GetComponentInChildren<UnityEngine.UI.Text>(true);
        buttonInfo = new {
            interactable = button.interactable,
            label = label != null ? label.text : null,
            label_font_size = label != null ? label.fontSize : 0,
            label_color = label != null ? Color(label.color) : null,
            label_visible = label != null && label.enabled && label.gameObject.activeInHierarchy && label.font != null && label.color.a > 0.01f
        };
    }

    object canvasInfo = null;
    if (canvas != null)
    {
        canvasInfo = new {
            render_mode = canvas.renderMode.ToString(),
            is_root = canvas.isRootCanvas,
            enabled = canvas.enabled
        };
    }

    object scalerInfo = null;
    if (scaler != null)
    {
        scalerInfo = new {
            scale_mode = scaler.uiScaleMode.ToString(),
            reference_resolution = V2(scaler.referenceResolution),
            match = scaler.matchWidthOrHeight
        };
    }

    nodes.Add(new {
        name = go.name,
        path = PathOf(go.transform),
        parent = go.transform.parent != null ? PathOf(go.transform.parent) : null,
        active = go.activeInHierarchy,
        rect,
        image = imageInfo,
        text = textInfo,
        button = buttonInfo,
        canvas = canvasInfo,
        scaler = scalerInfo
    });
}

var eventSystems = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.EventSystems.EventSystem>();
UnityEngine.EventSystems.EventSystem activeEventSystem = null;
var eventSystemCount = 0;
foreach (var candidate in eventSystems)
{
    if (candidate.gameObject.scene.IsValid() && candidate.gameObject.scene.handle == scene.handle && candidate.gameObject.activeInHierarchy)
    {
        eventSystemCount++;
        if (activeEventSystem == null) activeEventSystem = candidate;
    }
}

var raycasts = new System.Collections.Generic.List<object>();
foreach (var button in buttons)
{
    var rt = button.transform as UnityEngine.RectTransform;
    var targetPath = PathOf(button.transform);
    var reachable = false;
    string topPath = null;
    object screenPoint = null;
    if (rt != null && activeEventSystem != null)
    {
        var corners = new UnityEngine.Vector3[4];
        rt.GetWorldCorners(corners);
        var center = (corners[0] + corners[2]) * 0.5f;
        var canvas = button.GetComponentInParent<UnityEngine.Canvas>();
        UnityEngine.Camera camera = null;
        if (canvas != null && canvas.renderMode != UnityEngine.RenderMode.ScreenSpaceOverlay) camera = canvas.worldCamera;
        var screen = UnityEngine.RectTransformUtility.WorldToScreenPoint(camera, center);
        screenPoint = V2(screen);
        var pointer = new UnityEngine.EventSystems.PointerEventData(activeEventSystem) { position = screen };
        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        activeEventSystem.RaycastAll(pointer, results);
        if (results.Count > 0)
        {
            var top = results[0].gameObject;
            topPath = PathOf(top.transform);
            reachable = top == button.gameObject || top.transform.IsChildOf(button.transform);
        }
    }
    raycasts.Add(new { path = targetPath, reachable, top_path = topPath, screen_point = screenPoint });
}

return new {
    scene = scene.name,
    scene_path = scene.path,
    root_names = rootNames,
    root_canvas_count = rootCanvasCount,
    event_system_count = eventSystemCount,
    nodes,
    raycasts
};
