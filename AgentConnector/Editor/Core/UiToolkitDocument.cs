using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HeraAgent
{
    public static class UiToolkitDocument
    {
        public const string DefaultDirectory = "Assets/HeraGenerated/UI";

        public class ApplyResult
        {
            public readonly UiToolkitFixer.Profile FixerProfile = UiToolkitFixer.CurrentProfile();
            public readonly List<UiToolkitFixer.Report> Fixes = new List<UiToolkitFixer.Report>();
            public readonly List<UiToolkitFixer.Report> Diagnostics = new List<UiToolkitFixer.Report>();
            public readonly List<string> Errors = new List<string>();
            public readonly HashSet<string> ElementTypes = new HashSet<string>();
            public int Created;
            public int Updated;
            public int Elements;
            public string UxmlAsset;
            public string UssAsset;
            public string PanelSettingsAsset;
            public int RootId;
            public bool WorldSpace;
        }

        public static ApplyResult Apply(JObject document, Transform parent, bool upsert)
        {
            var result = new ApplyResult();
            UiToolkitFixer.ValidateDocument(document, result.Fixes, result.Diagnostics);
            if (UiToolkitFixer.HasErrors(result.Diagnostics))
            {
                CopyErrors(result.Diagnostics, result.Errors);
                return result;
            }
            if (!string.IsNullOrEmpty(UiToolkitStore.LoadError))
            {
                result.Errors.Add(UiToolkitStore.LoadError);
                return result;
            }

            var root = document?["root"] as JObject ?? document;
            if (root == null)
            {
                result.Errors.Add("UI Toolkit document has no root node.");
                return result;
            }
            if (!UiToolkitFixer.TryGetPanelRenderMode(document?["panel"] as JObject, out var renderMode, out var renderError))
            {
                result.Errors.Add(renderError);
                return result;
            }

            var stem = FileStem(document?["name"]?.ToString() ?? root["name"]?.ToString() ?? "HeraUiToolkit");
            if (!TryResolveAssetPaths(stem, upsert, out var uxmlPath, out var ussPath, out var panelPath, out var pathError))
            {
                result.Errors.Add(pathError);
                return result;
            }

            var emitter = new Emitter(Path.GetFileName(ussPath), CssStem(stem), result);
            var uxml = emitter.BuildUxml(root);
            var uss = emitter.Uss;
            if (!WriteAssets(uxmlPath, ussPath, uxml, uss, out var writeError))
            {
                result.Errors.Add(writeError);
                return result;
            }

            AssetDatabase.ImportAsset(ussPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(uxmlPath, ImportAssetOptions.ForceUpdate);
            var visualTree = AssetDatabase.LoadMainAssetAtPath(uxmlPath);
            if (visualTree == null)
            {
                result.Errors.Add($"Unity could not import generated UXML '{uxmlPath}'.");
                return result;
            }

            var panelSettings = CreateOrUpdatePanelSettings(panelPath, document?["panel"] as JObject, renderMode, out var panelError);
            if (panelSettings == null)
            {
                result.Errors.Add(panelError);
                return result;
            }

            var rootName = "HeraUITK_" + stem;
            var runtimeRoot = CreateOrUpdateDocument(rootName, parent, upsert, panelSettings, visualTree, out var documentError, out var created);
            if (runtimeRoot == null)
            {
                result.Errors.Add(documentError);
                return result;
            }

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = runtimeRoot;
            if (runtimeRoot.scene.IsValid()) EditorSceneManager.MarkSceneDirty(runtimeRoot.scene);

            result.UxmlAsset = uxmlPath;
            result.UssAsset = ussPath;
            result.PanelSettingsAsset = panelPath;
            result.RootId = EntityIdCompat.IdOf(runtimeRoot);
            result.WorldSpace = renderMode == "WorldSpace";
            if (created) result.Created++;
            else result.Updated++;
            return result;
        }

        public static bool TryMapManageUiElement(string requested, out string element)
        {
            element = null;
            if (string.IsNullOrWhiteSpace(requested)) return false;
            switch (requested.Trim().ToLowerInvariant())
            {
                case "canvas":
                case "panel":
                case "empty":
                    element = "VisualElement";
                    return true;
                case "image":
                    element = "Image";
                    return true;
                case "button":
                    element = "Button";
                    return true;
                case "text":
                    element = "Label";
                    return true;
            }

            if (UiToolkitStore.IsElement(requested))
            {
                element = requested;
                return true;
            }
            return false;
        }

        private static void CopyErrors(IList<UiToolkitFixer.Report> diagnostics, ICollection<string> errors)
        {
            foreach (var diagnostic in diagnostics)
                if (diagnostic != null && diagnostic.severity == "error") errors.Add(diagnostic.message);
        }

        private static bool TryResolveAssetPaths(string stem, bool upsert, out string uxmlPath, out string ussPath, out string panelPath, out string error)
        {
            uxmlPath = null;
            ussPath = null;
            panelPath = null;
            error = null;
            var candidate = stem;
            if (!upsert)
            {
                var suffix = 2;
                while (AssetPathExists(candidate))
                {
                    candidate = stem + "_" + suffix;
                    suffix++;
                }
            }

            if (!AssetPathGuard.TryNormalizeAssetFile(DefaultDirectory + "/" + candidate + ".uxml", out uxmlPath, out error)
                || !AssetPathGuard.TryNormalizeAssetFile(DefaultDirectory + "/" + candidate + ".uss", out ussPath, out error)
                || !AssetPathGuard.TryNormalizeAssetFile(DefaultDirectory + "/" + candidate + "PanelSettings.asset", out panelPath, out error))
            {
                return false;
            }
            return true;
        }

        private static bool AssetPathExists(string stem)
        {
            return File.Exists(ToFullPath(DefaultDirectory + "/" + stem + ".uxml"))
                || File.Exists(ToFullPath(DefaultDirectory + "/" + stem + ".uss"))
                || File.Exists(ToFullPath(DefaultDirectory + "/" + stem + "PanelSettings.asset"));
        }

        private static string ToFullPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        private static bool WriteAssets(string uxmlPath, string ussPath, string uxml, string uss, out string error)
        {
            error = null;
            try
            {
                var directory = Path.GetDirectoryName(ToFullPath(uxmlPath));
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(ToFullPath(ussPath), uss, new UTF8Encoding(false));
                File.WriteAllText(ToFullPath(uxmlPath), uxml, new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                error = $"failed to write generated UI Toolkit assets: {ex.Message}";
                return false;
            }
        }

        private static ScriptableObject CreateOrUpdatePanelSettings(string panelPath, JObject panelConfig, string renderMode, out string error)
        {
            error = null;
            var panelType = FindType("UnityEngine.UIElements.PanelSettings");
            if (panelType == null || !typeof(ScriptableObject).IsAssignableFrom(panelType))
            {
                error = "UI Toolkit PanelSettings is unavailable in this Editor.";
                return null;
            }

            var panel = AssetDatabase.LoadAssetAtPath<ScriptableObject>(panelPath);
            try
            {
                if (panel == null)
                {
                    panel = ScriptableObject.CreateInstance(panelType);
                    AssetDatabase.CreateAsset(panel, panelPath);
                }
                else if (!panelType.IsInstanceOfType(panel))
                {
                    error = $"'{panelPath}' is not a PanelSettings asset.";
                    return null;
                }

                SetPanelRenderMode(panel, renderMode);
                ApplyReferenceResolution(panel, panelConfig?["reference_resolution"] as JArray);
                EditorUtility.SetDirty(panel);
                return panel;
            }
            catch (Exception ex)
            {
                error = $"failed to create PanelSettings: {ex.Message}";
                return null;
            }
        }

        private static GameObject CreateOrUpdateDocument(string rootName, Transform parent, bool upsert, ScriptableObject panelSettings, UnityEngine.Object visualTree, out string error, out bool created)
        {
            error = null;
            created = false;
            var documentType = ComponentTypeResolver.Resolve("UIDocument");
            if (documentType == null)
            {
                error = "UI Toolkit UIDocument is unavailable in this Editor.";
                return null;
            }

            try
            {
                var root = upsert ? FindSceneObject(rootName) : null;
                if (root == null)
                {
                    root = new GameObject(rootName);
                    Undo.RegisterCreatedObjectUndo(root, "Hera UI Toolkit document");
                    created = true;
                }
                if (parent != null) root.transform.SetParent(parent, false);

                var document = root.GetComponent(documentType) ?? root.AddComponent(documentType);
                SetObjectProperty(document, "panelSettings", panelSettings);
                SetObjectProperty(document, "visualTreeAsset", visualTree);
                EditorUtility.SetDirty(document);
                return root;
            }
            catch (Exception ex)
            {
                error = $"failed to create UIDocument: {ex.Message}";
                return null;
            }
        }

        private static GameObject FindSceneObject(string name)
        {
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
                if (candidate.scene.IsValid() && candidate.name == name) return candidate;
            return null;
        }

        private static void SetPanelRenderMode(object target, string renderMode)
        {
            var property = target.GetType().GetProperty("renderMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null && string.Equals(renderMode, "ScreenSpaceOverlay", StringComparison.Ordinal)) return;
            if (property == null || !property.CanWrite || !property.PropertyType.IsEnum)
                throw new InvalidOperationException($"{target.GetType().Name}.renderMode is unavailable.");
            property.SetValue(target, Enum.Parse(property.PropertyType, renderMode, true));
        }

        private static void ApplyReferenceResolution(object target, JArray resolution)
        {
            if (resolution == null || resolution.Count != 2) return;
            var property = target.GetType().GetProperty("referenceResolution", BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanWrite) return;
            var width = resolution[0].Value<int>();
            var height = resolution[1].Value<int>();
            property.SetValue(target, Activator.CreateInstance(property.PropertyType, width, height));
        }

        private static void SetObjectProperty(object target, string name, UnityEngine.Object value)
        {
            var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanWrite || !property.PropertyType.IsInstanceOfType(value))
                throw new InvalidOperationException($"{target.GetType().Name}.{name} is unavailable.");
            property.SetValue(target, value);
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, throwOnError: false);
                if (type != null) return type;
            }
            return null;
        }

        private static string FileStem(string value)
        {
            var builder = new StringBuilder();
            foreach (var c in value ?? string.Empty)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') builder.Append(c);
                else if (builder.Length == 0 || builder[builder.Length - 1] != '_') builder.Append('_');
            }
            return builder.Length == 0 ? "HeraUiToolkit" : builder.ToString();
        }

        private static string CssStem(string value)
        {
            var builder = new StringBuilder();
            foreach (var c in value)
            {
                if (char.IsLetterOrDigit(c)) builder.Append(char.ToLowerInvariant(c));
                else if (builder.Length == 0 || builder[builder.Length - 1] != '-') builder.Append('-');
            }
            return builder.Length == 0 ? "ui" : builder.ToString().Trim('-');
        }

        private sealed class Emitter
        {
            private readonly string _ussFileName;
            private readonly string _cssStem;
            private readonly ApplyResult _result;
            private readonly StringBuilder _uxml = new StringBuilder();
            private readonly StringBuilder _uss = new StringBuilder();
            private int _nodeIndex;

            public Emitter(string ussFileName, string cssStem, ApplyResult result)
            {
                _ussFileName = ussFileName;
                _cssStem = cssStem;
                _result = result;
            }

            public string Uss => _uss.ToString();

            public string BuildUxml(JObject root)
            {
                _uxml.AppendLine("<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">");
                _uxml.Append("  <Style src=\"").Append(EscapeXml(_ussFileName)).AppendLine("\" />");
                EmitNode(root, 1, true);
                _uxml.AppendLine("</ui:UXML>");
                return _uxml.ToString();
            }

            private void EmitNode(JObject node, int depth, bool isRoot)
            {
                var elementName = node["element"].ToString();
                var cssClass = "hera-" + _cssStem + "-" + _nodeIndex;
                _nodeIndex++;
                _result.Elements++;
                _result.ElementTypes.Add(elementName);

                AppendStyles(cssClass, node["style"] as JObject, isRoot);
                var indent = new string(' ', depth * 2);
                _uxml.Append(indent).Append("<ui:").Append(elementName);
                AppendAttribute("class", cssClass);
                var nodeName = node["name"] ?? (node["attributes"] as JObject)?["name"];
                if (nodeName != null) AppendAttribute("name", Scalar(nodeName));
                if (node["attributes"] is JObject attributes)
                {
                    foreach (var property in attributes.Properties())
                    {
                        if (property.Name == "name" || !IsEmittableAttribute(elementName, property)) continue;
                        AppendAttribute(property.Name, Scalar(property.Value));
                    }
                }

                var children = node["children"] as JArray;
                if (children == null || children.Count == 0)
                {
                    _uxml.AppendLine(" />");
                    return;
                }
                _uxml.AppendLine(">");
                foreach (var child in children)
                    EmitNode((JObject)child, depth + 1, false);
                _uxml.Append(indent).Append("</ui:").Append(elementName).AppendLine(">");
            }

            private bool IsEmittableAttribute(string elementName, JProperty property)
            {
                if (property.Name == "class" || property.Value == null) return false;
                var element = UiToolkitStore.GetElement(elementName);
                if (element?.attributes == null) return false;
                foreach (var attribute in element.attributes)
                    if (attribute != null && attribute.name == property.Name
                        && property.Value.Type != JTokenType.Array && property.Value.Type != JTokenType.Object && property.Value.Type != JTokenType.Null)
                        return true;
                return false;
            }

            private void AppendStyles(string cssClass, JObject styles, bool isRoot)
            {
                var properties = new List<KeyValuePair<string, string>>();
                if (isRoot && (styles == null || styles["flex-direction"] == null) && UiToolkitStore.IsUssProperty("flex-direction"))
                    properties.Add(new KeyValuePair<string, string>("flex-direction", "column"));
                if (styles != null)
                {
                    foreach (var property in styles.Properties())
                    {
                        if (!UiToolkitStore.IsUssProperty(property.Name) || !UiToolkitFixer.IsSafeUssValue(property.Value))
                            continue;
                        properties.Add(new KeyValuePair<string, string>(property.Name, Scalar(property.Value)));
                    }
                }
                if (properties.Count == 0) return;

                _uss.Append('.').Append(cssClass).AppendLine(" {");
                foreach (var property in properties)
                    _uss.Append("  ").Append(property.Key).Append(": ").Append(property.Value).AppendLine(";");
                _uss.AppendLine("}");
                _uss.AppendLine();
            }

            private void AppendAttribute(string name, string value)
            {
                _uxml.Append(' ').Append(name).Append("=\"").Append(EscapeXml(value)).Append('"');
            }

            private static string Scalar(JToken value)
            {
                if (value.Type == JTokenType.String) return value.Value<string>();
                if (value.Type == JTokenType.Boolean) return value.Value<bool>() ? "true" : "false";
                return value.ToString(Formatting.None);
            }

            private static string EscapeXml(string value)
            {
                return (value ?? string.Empty)
                    .Replace("&", "&amp;")
                    .Replace("\"", "&quot;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;");
            }
        }
    }
}
