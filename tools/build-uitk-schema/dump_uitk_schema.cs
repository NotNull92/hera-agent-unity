// dump_uitk_schema.cs — in-editor reflection dump of the UI Toolkit surface.
//
// UI Toolkit runtime is the built-in UnityEngine.UIElementsModule engine module,
// so (unlike com.unity.ugui) there is no package.json version to bucket on. The
// precise per-version surface lives only in the module assemblies, so a
// maintainer runs this snippet inside each installed Editor via:
//
//     hera-agent-unity exec --file tools/build-uitk-schema/dump_uitk_schema.cs
//
// It writes uitk_schema_<bucket>.jsonl to the Editor temp cache and returns the
// path + counts. Feed that file to `go run ./tools/build-uitk-schema` to validate
// and gzip it into AgentConnector/Editor/Data/uitk_schema_<bucket>.jsonl.gz.bytes.
//
// The bucket is derived the same way as HeraAgent.UnityVersionCompat (which is
// internal, so it cannot be called across the exec assembly boundary — the tiny
// mapping is replicated here).
//
// JSONL line shapes (loaded by UiToolkitStore, mirroring UnityDocsStore):
//   {"kind":"meta","unity_version":"6000.3.5f2","bucket":"6000.3",
//    "uxml_element_attribute":true,"uxml_traits":"obsolete","elements":N,"uss_properties":M}
//   {"kind":"element","element":"Button","full_type":"UnityEngine.UIElements.Button",
//    "surface":"runtime","attributes":[{"name":"text","type":"string","default":""}]}
//   {"kind":"uss","name":"flex-direction","animatable":true}

const System.Reflection.BindingFlags SPNP =
    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
const System.Reflection.BindingFlags INST =
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

var asms = System.AppDomain.CurrentDomain.GetAssemblies();
System.Func<string, System.Type> find = fn => {
    foreach (var a in asms) { var t = a.GetType(fn); if (t != null) return t; }
    return null;
};

// --- bucket (mirror of UnityVersionCompat.DocsVersionFor) ---
var unityVersion = UnityEngine.Application.unityVersion;
System.Func<string, string> bucketFor = uv => {
    var parts = uv.Split('.');
    int major = 0, minor = 0;
    if (parts.Length >= 2) { int.TryParse(new string(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.TakeWhile(parts[0], char.IsDigit))), out major); int.TryParse(new string(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.TakeWhile(parts[1], char.IsDigit))), out minor); }
    if (major == 2022) return "2022.3";
    if (major == 2023) return "2023.2";
    if (major == 6000) { if (minor >= 5) return "6000.5"; if (minor >= 3) return "6000.3"; return "6000.0"; }
    return "6000.0";
};
var bucket = bucketFor(unityVersion);

// --- JSON string escaper (dependency-free; no Newtonsoft compile ref needed) ---
System.Func<string, string> esc = s => {
    if (s == null) return "";
    var sb2 = new System.Text.StringBuilder(s.Length + 8);
    foreach (var c in s) {
        switch (c) {
            case '"': sb2.Append("\\\""); break;
            case '\\': sb2.Append("\\\\"); break;
            case '\n': sb2.Append("\\n"); break;
            case '\r': sb2.Append("\\r"); break;
            case '\t': sb2.Append("\\t"); break;
            default: if (c < 0x20) sb2.Append("\\u").Append(((int)c).ToString("x4")); else sb2.Append(c); break;
        }
    }
    return sb2.ToString();
};

// --- attribute type from description class name ---
// "UxmlStringAttributeDescription" -> "string"; "UxmlEnumAttributeDescription`1" -> "enum"
System.Func<string, string> attrType = n => {
    int bt = n.IndexOf('`'); if (bt >= 0) n = n.Substring(0, bt);
    if (n.StartsWith("Uxml")) n = n.Substring(4);
    const string suf = "AttributeDescription";
    if (n.EndsWith(suf)) n = n.Substring(0, n.Length - suf.Length);
    return n.ToLowerInvariant();
};

// --- surface from uxml namespace ---
System.Func<string, string> surfaceFor = ns => {
    if (string.IsNullOrEmpty(ns)) return "other";
    if (ns.StartsWith("UnityEditor")) return "editor";
    if (ns.StartsWith("UnityEngine")) return "runtime";
    return "other";
};

var veType = find("UnityEngine.UIElements.VisualElement");
var lines = new System.Collections.Generic.List<string>();

// Runtime UI Toolkit elements only. UnityEngine.UIElementsModule is the built-in
// runtime element library — deterministic across projects and Unity versions, and
// exactly the surface usable in runtime game UI (the emitter's v1 scope). The
// factory registry also holds editor controls (UnityEditor.CoreModule, mixed with
// version-variable editor internals + UI Builder), package elements (Shader Graph,
// Tilemap, GraphView) and project custom controls; all are excluded here. Editor
// UI generation, if ever added, needs a curated set — assembly alone is too noisy.
var builtinAsm = new System.Collections.Generic.HashSet<string> {
    "UnityEngine.UIElementsModule"
};

// ---- elements ----
// Two Unity architectures, branched on RegisterEngineFactories presence:
//  * Classic (2022.3 .. 6000.3): VisualElementFactoryRegistry is fully populated
//    by RegisterEngineFactories, and each factory carries its uxml attributes.
//  * New (6000.5+): RegisterEngineFactories is gone, the registry is only lazily/
//    partially populated, and standalone factories report no attributes (traits
//    are empty shells). Enumerate module VisualElement types that still carry a
//    nested UxmlFactory (deterministic, matches the classic element set) and read
//    attributes from UxmlDescriptionRegistry keyed by the element's nested
//    UxmlSerializedData type.
var factoryReg = find("UnityEngine.UIElements.VisualElementFactoryRegistry");
var registerEngine = factoryReg?.GetMethod("RegisterEngineFactories", SPNP);

int elementCount = 0;
int structuralCount = 0;

// A created type of exactly VisualElement with a tag other than "VisualElement"
// is a UXML structural directive (UXML/Template/Style/AttributeOverrides), not a
// placeable control — classify it apart so the allow-list stays clean.
System.Action<string, string, System.Type, string> emit = (uxmlName, uxmlNs, createdType, attrsJson) => {
    bool structural = createdType == veType && uxmlName != "VisualElement";
    lines.Add("{\"kind\":\"" + (structural ? "structural" : "element") + "\",\"element\":\"" + esc(uxmlName)
        + "\",\"full_type\":\"" + esc(createdType.FullName)
        + "\",\"surface\":\"" + surfaceFor(uxmlNs)
        + "\",\"attributes\":[" + attrsJson + "]}");
    if (structural) structuralCount++; else elementCount++;
};
System.Func<string, string, string, string> attrJson = (name, type, def) =>
    "{\"name\":\"" + esc(name) + "\",\"type\":\"" + esc(type) + "\",\"default\":\"" + esc(def) + "\"}";

if (registerEngine != null) {
    // --- Classic path (2022.3 .. 6000.3) ---
    try { registerEngine.Invoke(null, null); } catch { }
    var factories = factoryReg.GetProperty("factories", SPNP).GetValue(null) as System.Collections.IDictionary;
    if (factories != null) {
        foreach (var key in factories.Keys) {
            object factory = null;
            foreach (var f in (System.Collections.IEnumerable)factories[key]) { factory = f; break; }
            if (factory == null) continue;
            var ft = factory.GetType();
            var uxmlType = ft.GetProperty("uxmlType", INST)?.GetValue(factory) as System.Type;
            if (uxmlType == null || !veType.IsAssignableFrom(uxmlType)) continue;
            if (!builtinAsm.Contains(uxmlType.Assembly.GetName().Name)) continue;
            var uxmlName = ft.GetProperty("uxmlName", INST)?.GetValue(factory)?.ToString();
            var uxmlNs = ft.GetProperty("uxmlNamespace", INST)?.GetValue(factory)?.ToString();
            if (string.IsNullOrEmpty(uxmlName)) continue;
            var attrSb = new System.Text.StringBuilder();
            bool firstAttr = true;
            var descs = ft.GetProperty("uxmlAttributesDescription", INST)?.GetValue(factory) as System.Collections.IEnumerable;
            if (descs != null) {
                foreach (var d in descs) {
                    if (d == null) continue;
                    var dt = d.GetType();
                    var an = dt.GetProperty("name", INST)?.GetValue(d)?.ToString();
                    if (string.IsNullOrEmpty(an)) continue;
                    var adef = dt.GetProperty("defaultValueAsString", INST)?.GetValue(d)?.ToString() ?? "";
                    if (!firstAttr) attrSb.Append(',');
                    firstAttr = false;
                    attrSb.Append(attrJson(an, attrType(dt.Name), adef));
                }
            }
            emit(uxmlName, uxmlNs, uxmlType, attrSb.ToString());
        }
    }
} else {
    // --- New path (6000.5+) ---
    var reg = find("UnityEngine.UIElements.UxmlDescriptionRegistry");
    var getDesc = reg?.GetMethod("GetDescription", SPNP);
    System.Func<System.Type, string> clrType = ct => {
        if (ct == null) return "string";
        if (ct.IsEnum) return "enum";
        switch (ct.FullName) {
            case "System.String": return "string";
            case "System.Boolean": return "bool";
            case "System.Int32": return "int";
            case "System.UInt32": return "unsignedint";
            case "System.Int64": return "long";
            case "System.UInt64": return "unsignedlong";
            case "System.Single": return "float";
            case "System.Double": return "double";
            case "System.Type": return "type";
        }
        if (typeof(UnityEngine.Object).IsAssignableFrom(ct)) return "asset";
        var nm = ct.Name;
        int bt = nm.IndexOf('`'); if (bt >= 0) nm = nm.Substring(0, bt);
        return nm.ToLowerInvariant();
    };
    foreach (var t in veType.Assembly.GetTypes()) {
        if (t == null || t.IsAbstract || !veType.IsAssignableFrom(t)) continue;
        var factoryType = t.GetNestedType("UxmlFactory", INST);
        if (factoryType == null) continue; // not a UXML-instantiable element
        string uxmlName = t.Name, uxmlNs = t.Namespace;
        try {
            var fInst = System.Activator.CreateInstance(factoryType);
            uxmlName = factoryType.GetProperty("uxmlName", INST)?.GetValue(fInst)?.ToString() ?? t.Name;
            uxmlNs = factoryType.GetProperty("uxmlNamespace", INST)?.GetValue(fInst)?.ToString() ?? t.Namespace;
        } catch { }
        if (string.IsNullOrEmpty(uxmlName)) continue;
        var attrSb = new System.Text.StringBuilder();
        bool firstAttr = true;
        var sd = t.GetNestedType("UxmlSerializedData", INST);
        if (sd != null && !sd.IsAbstract && getDesc != null) {
            object desc = null, sdInstance = null, elemInstance = null;
            try { desc = getDesc.Invoke(null, new object[] { sd }); } catch { }
            try { sdInstance = System.Activator.CreateInstance(sd); } catch { }
            try { elemInstance = System.Activator.CreateInstance(t); } catch { }
            var attrs = desc?.GetType().GetField("attributeDescriptions", INST)?.GetValue(desc) as System.Collections.IEnumerable;
            if (attrs != null) {
                foreach (var a in attrs) {
                    if (a == null) continue;
                    var at = a.GetType();
                    var an = at.GetField("uxmlName", INST)?.GetValue(a)?.ToString();
                    if (string.IsNullOrEmpty(an)) continue;
                    var typeStr = clrType(at.GetField("fieldType", INST)?.GetValue(a) as System.Type);
                    // Prefer the element's real default (constructor-set) read via the
                    // attribute's C# member name; the serialized field's initial value
                    // (e.g. Slider.highValue = 0) misses constructor defaults (= 10).
                    var csName = at.GetField("cSharpName", INST)?.GetValue(a)?.ToString();
                    string adef = "";
                    if (elemInstance != null && !string.IsNullOrEmpty(csName)) {
                        try {
                            var pi = t.GetProperty(csName, INST);
                            var dv = pi != null ? pi.GetValue(elemInstance) : t.GetField(csName, INST)?.GetValue(elemInstance);
                            if (dv != null) adef = dv.ToString();
                        } catch { }
                    }
                    if (adef.Length == 0) {
                        var sfield = at.GetField("serializedField", INST)?.GetValue(a) as System.Reflection.FieldInfo;
                        if (sfield != null && sdInstance != null) { try { adef = sfield.GetValue(sdInstance)?.ToString() ?? ""; } catch { } }
                    }
                    if (typeStr == "bool") adef = adef.ToLowerInvariant();
                    if (!firstAttr) attrSb.Append(',');
                    firstAttr = false;
                    attrSb.Append(attrJson(an, typeStr, adef));
                }
            }
        }
        emit(uxmlName, uxmlNs, t, attrSb.ToString());
    }
}

// ---- USS properties via StylePropertyUtil.s_IdToName ----
// s_IdToName (Dictionary<StylePropertyId, string>) is the version-robust source:
// present in both 2022.3 and 6000.x, values are canonical USS kebab names, and
// non-property sentinels (Unknown/Custom) are already excluded. The newer
// ussNameToCSharpName property does not exist in 2022.3.
var spu = find("UnityEngine.UIElements.StyleSheets.StylePropertyUtil");
var idToName = spu?.GetField("s_IdToName", SPNP)?.GetValue(null) as System.Collections.IDictionary;
var isAnim = spu?.GetMethod("IsAnimatable", SPNP);
int ussCount = 0;
if (idToName != null) {
    foreach (var id in idToName.Keys) {
        var ussName = idToName[id]?.ToString();
        if (string.IsNullOrEmpty(ussName)) continue;
        bool animatable = false;
        if (isAnim != null) { try { animatable = (bool)isAnim.Invoke(null, new object[] { id }); } catch { } }
        lines.Add("{\"kind\":\"uss\",\"name\":\"" + esc(ussName) + "\",\"animatable\":" + (animatable ? "true" : "false") + "}");
        ussCount++;
    }
}

// ---- meta (first line) ----
var uxmlElemAttr = find("UnityEngine.UIElements.UxmlElementAttribute") != null;
var uxmlTraits = find("UnityEngine.UIElements.UxmlTraits");
var traitsState = uxmlTraits == null ? "absent"
    : (uxmlTraits.GetCustomAttributes(typeof(System.ObsoleteAttribute), false).Length > 0 ? "obsolete" : "present");

var meta = "{\"kind\":\"meta\",\"unity_version\":\"" + esc(unityVersion)
    + "\",\"bucket\":\"" + bucket
    + "\",\"uxml_element_attribute\":" + (uxmlElemAttr ? "true" : "false")
    + ",\"uxml_traits\":\"" + traitsState
    + "\",\"elements\":" + elementCount
    + ",\"structural\":" + structuralCount
    + ",\"uss_properties\":" + ussCount + "}";

var sb = new System.Text.StringBuilder();
sb.Append(meta).Append('\n');
foreach (var l in lines) sb.Append(l).Append('\n');

var outPath = System.IO.Path.Combine(UnityEngine.Application.temporaryCachePath, "uitk_schema_" + bucket + ".jsonl");
System.IO.File.WriteAllText(outPath, sb.ToString());

return new { path = outPath, bucket = bucket, unity_version = unityVersion, elements = elementCount, uss_properties = ussCount, uxml_traits = traitsState };
