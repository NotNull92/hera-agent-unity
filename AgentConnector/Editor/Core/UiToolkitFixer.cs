using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HeraAgent
{
    public static class UiToolkitFixer
    {
        public class Profile
        {
            public string uitk_version;
            public string uxml_traits;
            public string uxml_api;
            public string manual_url;
        }

        public class Report
        {
            public string rule;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string severity;

            public string path;
            public string message;
        }

        public static Profile CurrentProfile()
        {
            var bucket = UiToolkitStore.LoadedBucket ?? UnityVersionCompat.CurrentDocsVersion();
            var traits = UiToolkitStore.UxmlTraits ?? "unknown";
            return new Profile
            {
                uitk_version = bucket,
                uxml_traits = traits,
                uxml_api = UiToolkitStore.SupportsUxmlElementAttribute ? "source-generated" : "traits",
                manual_url = ManualUrlForBucket(bucket),
            };
        }

        public static void ValidateDocument(JObject document, IList<Report> fixes, IList<Report> diagnostics)
        {
            if (document == null)
            {
                Add(diagnostics, "uitk.document.missing", "error", "/", "UI Toolkit document is missing.");
                return;
            }

            var panelToken = document["panel"];
            if (panelToken != null && panelToken.Type != JTokenType.Object)
                Add(diagnostics, "uitk.panel.invalid", "error", "/panel", "UI Toolkit panel must be an object.");
            else
                ValidatePanel(panelToken as JObject, diagnostics);
            var root = document["root"] as JObject ?? document;
            ValidateNode(root, "/", diagnostics);
        }

        public static bool HasErrors(IList<Report> diagnostics)
        {
            if (diagnostics == null) return false;
            foreach (var diagnostic in diagnostics)
                if (diagnostic != null && diagnostic.severity == "error") return true;
            return false;
        }

        public static bool TryGetPanelRenderMode(JObject panel, out string renderMode, out string error)
        {
            renderMode = "ScreenSpaceOverlay";
            error = null;
            var requested = panel?["render_mode"]?.ToString() ?? "screen-space";
            switch (requested.Trim().ToLowerInvariant().Replace("_", "-").Replace(" ", "-"))
            {
                case "screen":
                case "screen-space":
                case "screen-space-overlay":
                    return true;
                case "world":
                case "world-space":
                    if (!SupportsWorldSpaceRuntime(Application.unityVersion))
                    {
                        error = $"world-space UI Toolkit needs Unity runtime 6000.2 or newer (running {Application.unityVersion}).";
                        return false;
                    }
                    renderMode = "WorldSpace";
                    return true;
                default:
                    error = $"unknown panel.render_mode '{requested}'; use screen-space or world-space.";
                    return false;
            }
        }

        public static bool SupportsWorldSpaceRuntime(string unityVersion)
        {
            if (!TryParseMajorMinor(unityVersion, out var major, out var minor)) return false;
            return major > 6000 || (major == 6000 && minor >= 2);
        }

        private static void ValidatePanel(JObject panel, IList<Report> diagnostics)
        {
            if (TryGetPanelRenderMode(panel, out _, out var error)) return;
            Add(diagnostics, "uitk.panel.render_mode", "error", "/panel/render_mode", error);
        }

        private static void ValidateNode(JObject node, string path, IList<Report> diagnostics)
        {
            if (node == null)
            {
                Add(diagnostics, "uitk.node.invalid", "error", path, "Each UI Toolkit node must be an object.");
                return;
            }

            var elementName = node["element"]?.ToString();
            if (string.IsNullOrWhiteSpace(elementName))
            {
                Add(diagnostics, "uitk.element.missing", "error", path, "A UI Toolkit node needs an exact runtime element name.");
            }
            else
            {
                var element = UiToolkitStore.GetElement(elementName);
                if (element == null)
                {
                    var suggestions = UiToolkitStore.SuggestElements(elementName);
                    var suffix = suggestions.Count == 0 ? "" : " Try: " + string.Join(", ", suggestions) + ".";
                    Add(diagnostics, "uitk.element.unsupported", "error", path,
                        $"'{elementName}' is not a runtime UI Toolkit element in bucket {UiToolkitStore.LoadedBucket}.{suffix}");
                }
                else
                {
                    ValidateName(node, element, path, diagnostics);
                    var attributes = node["attributes"];
                    if (attributes != null && attributes.Type != JTokenType.Object)
                        Add(diagnostics, "uitk.attributes.invalid", "error", path + "attributes", "UI Toolkit attributes must be an object.");
                    else
                        ValidateAttributes(attributes as JObject, element, path, diagnostics);
                }
            }

            var styles = node["style"];
            if (styles != null && styles.Type != JTokenType.Object)
                Add(diagnostics, "uitk.style.invalid", "error", path + "style", "UI Toolkit style must be an object.");
            else
                ValidateStyles(styles as JObject, path, diagnostics);

            var childrenToken = node["children"];
            if (childrenToken != null && childrenToken.Type != JTokenType.Array)
                Add(diagnostics, "uitk.children.invalid", "error", path + "children", "UI Toolkit children must be an array.");
            else if (childrenToken is JArray children)
            {
                for (var i = 0; i < children.Count; i++)
                    ValidateNode(children[i] as JObject, path + "children/" + i + "/", diagnostics);
            }
        }

        private static void ValidateName(JObject node, UiToolkitStore.Element element, string path, IList<Report> diagnostics)
        {
            if (node["name"] == null) return;
            if (!HasAttribute(element, "name"))
                Add(diagnostics, "uitk.attribute.unsupported", "error", path + "name",
                    $"'{element.element}' does not expose the UXML 'name' attribute in bucket {UiToolkitStore.LoadedBucket}.");
        }

        private static void ValidateAttributes(JObject attributes, UiToolkitStore.Element element, string path, IList<Report> diagnostics)
        {
            if (attributes == null) return;
            foreach (var property in attributes.Properties())
            {
                var attributePath = path + "attributes/" + property.Name;
                if (string.Equals(property.Name, "class", StringComparison.Ordinal))
                {
                    Add(diagnostics, "uitk.attribute.class_managed", "error", attributePath,
                        "Classes are generated as .hera-* selectors; put layout in style instead of attributes.class.");
                    continue;
                }
                if (IsDataBindingAttribute(property.Name))
                {
                    Add(diagnostics, "uitk.binding.out_of_scope", "error", attributePath,
                        "MVVM and data-binding attributes are outside the UI Toolkit v1 layout-scaffolding scope.");
                    continue;
                }
                if (!HasAttribute(element, property.Name))
                {
                    Add(diagnostics, "uitk.attribute.unsupported", "error", attributePath,
                        $"'{property.Name}' is not a reflected UXML attribute of '{element.element}' in bucket {UiToolkitStore.LoadedBucket}.");
                    continue;
                }
                if (!IsScalar(property.Value))
                    Add(diagnostics, "uitk.attribute.value", "error", attributePath,
                        "UXML attributes must be scalar values in a UI Toolkit layout document.");
            }
        }

        private static void ValidateStyles(JObject styles, string path, IList<Report> diagnostics)
        {
            if (styles == null) return;
            foreach (var property in styles.Properties())
            {
                if (UiToolkitStore.IsUssProperty(property.Name)) continue;
                var suggestions = UiToolkitStore.SuggestUss(property.Name);
                var suffix = suggestions.Count == 0 ? "" : " Try: " + string.Join(", ", suggestions) + ".";
                Add(diagnostics, "uitk.uss.unsupported", "warning", path + "style/" + property.Name,
                    $"'{property.Name}' is not a reflected USS property in bucket {UiToolkitStore.LoadedBucket}; it will not be emitted.{suffix}");
            }

            foreach (var property in styles.Properties())
            {
                if (!UiToolkitStore.IsUssProperty(property.Name) || IsSafeUssValue(property.Value)) continue;
                Add(diagnostics, "uitk.uss.value", "error", path + "style/" + property.Name,
                    "USS style values must be one scalar declaration; semicolons, braces, and line breaks are not allowed.");
            }
        }

        private static bool HasAttribute(UiToolkitStore.Element element, string name)
        {
            if (element?.attributes == null) return false;
            foreach (var attribute in element.attributes)
                if (attribute != null && string.Equals(attribute.name, name, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsDataBindingAttribute(string name)
        {
            return !string.IsNullOrEmpty(name)
                && (name.StartsWith("data-source", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("binding", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "bindings", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsScalar(JToken value)
        {
            return value != null && value.Type != JTokenType.Array && value.Type != JTokenType.Object && value.Type != JTokenType.Null;
        }

        public static bool IsSafeUssValue(JToken value)
        {
            if (!IsScalar(value)) return false;
            if (value.Type != JTokenType.String) return true;
            return value.Value<string>().IndexOfAny(new[] { ';', '{', '}', '\r', '\n' }) < 0;
        }

        private static string ManualUrlForBucket(string bucket)
        {
            switch (bucket)
            {
                case UnityVersionCompat.Docs2022_3: return "https://docs.unity3d.com/kr/2022.3/Manual/UIElements.html";
                case UnityVersionCompat.Docs2023_2: return "https://docs.unity3d.com/kr/2023.2/Manual/UIElements.html";
                case UnityVersionCompat.Docs6000_3: return "https://docs.unity3d.com/6000.3/Documentation/Manual/UIElements.html";
                case UnityVersionCompat.Docs6000_5: return "https://docs.unity3d.com/6000.5/Documentation/Manual/UIElements.html";
                default: return "https://docs.unity3d.com/6000.1/Documentation/Manual/UIElements.html";
            }
        }

        private static bool TryParseMajorMinor(string unityVersion, out int major, out int minor)
        {
            major = 0;
            minor = 0;
            if (string.IsNullOrEmpty(unityVersion)) return false;
            var parts = unityVersion.Split('.');
            return parts.Length >= 2 && TryParseLeadingInt(parts[0], out major) && TryParseLeadingInt(parts[1], out minor);
        }

        private static bool TryParseLeadingInt(string value, out int result)
        {
            result = 0;
            if (string.IsNullOrEmpty(value)) return false;
            var end = 0;
            while (end < value.Length && char.IsDigit(value[end])) end++;
            return end > 0 && int.TryParse(value.Substring(0, end), out result);
        }

        private static void Add(IList<Report> reports, string rule, string severity, string path, string message)
        {
            if (reports == null) return;
            reports.Add(new Report { rule = rule, severity = severity, path = path, message = message });
        }
    }
}
