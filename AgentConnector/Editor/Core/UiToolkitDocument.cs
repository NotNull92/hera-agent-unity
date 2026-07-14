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
        internal const string FailureStageAfterAssets = "after-assets";
        internal const string FailureStageAfterPanelSettings = "after-panel-settings";
        internal const string FailureStageAfterDocument = "after-document";

        internal static Func<string, Exception> FailureInjectionForTests;

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
            public bool RollbackAttempted;
            public readonly List<string> RolledBackArtifacts = new List<string>();
            public readonly List<string> RollbackErrors = new List<string>();
            public bool UpsertMayBePartial;
        }

        public static ApplyResult Apply(JObject document, Transform parent, bool upsert)
        {
            var result = new ApplyResult();
            var createdAssets = new List<string>();
            GameObject createdRoot = null;
            var mutationsStarted = false;
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

            if (!TryPreflight(document, panelPath, out var preflight, out var preflightError))
            {
                result.Errors.Add(preflightError);
                return result;
            }

            string uxml;
            string uss;
            try
            {
                var emitter = new Emitter(Path.GetFileName(ussPath), CssStem(stem), result);
                uxml = emitter.BuildUxml(root);
                uss = emitter.Uss;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"failed to generate UI Toolkit markup: {ex.Message}");
                return result;
            }

            try
            {
                mutationsStarted = true;
                if (!WriteAssets(uxmlPath, ussPath, uxml, uss, !upsert, createdAssets, out var writeError))
                    return FailAfterMutation(result, writeError, upsert, mutationsStarted, createdAssets, createdRoot);

                ThrowIfFailureInjected(FailureStageAfterAssets);
                AssetDatabase.ImportAsset(ussPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(uxmlPath, ImportAssetOptions.ForceUpdate);
                var visualTree = AssetDatabase.LoadMainAssetAtPath(uxmlPath);
                if (visualTree == null)
                    return FailAfterMutation(result, $"Unity could not import generated UXML '{uxmlPath}'.", upsert, mutationsStarted, createdAssets, createdRoot);

                var panelSettings = CreateOrUpdatePanelSettings(preflight.PanelType, panelPath, document?["panel"] as JObject, renderMode, out var panelError, out var panelCreated);
                if (panelCreated) createdAssets.Add(panelPath);
                if (panelSettings == null)
                    return FailAfterMutation(result, panelError, upsert, mutationsStarted, createdAssets, createdRoot);

                ThrowIfFailureInjected(FailureStageAfterPanelSettings);
                var rootName = "HeraUITK_" + stem;
                var runtimeRoot = CreateOrUpdateDocument(preflight.DocumentType, rootName, parent, upsert, panelSettings, visualTree, out var documentError, out var created, out createdRoot);
                if (runtimeRoot == null)
                    return FailAfterMutation(result, documentError, upsert, mutationsStarted, createdAssets, createdRoot);

                ThrowIfFailureInjected(FailureStageAfterDocument);
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
            catch (Exception ex)
            {
                return FailAfterMutation(result, $"failed to emit UI Toolkit document: {ex.Message}", upsert, mutationsStarted, createdAssets, createdRoot);
            }
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

        private sealed class ApplyPreflight
        {
            public Type PanelType;
            public Type DocumentType;
        }

        private static bool TryPreflight(JObject document, string panelPath, out ApplyPreflight preflight, out string error)
        {
            preflight = null;
            error = null;
            if (File.Exists(ToFullPath(DefaultDirectory)))
            {
                error = $"generated UI Toolkit path '{DefaultDirectory}' is a file.";
                return false;
            }

            var panelType = FindType("UnityEngine.UIElements.PanelSettings");
            if (panelType == null || !typeof(ScriptableObject).IsAssignableFrom(panelType))
            {
                error = "UI Toolkit PanelSettings is unavailable in this Editor.";
                return false;
            }
            if (!CanSetPanelRenderMode(panelType, document?["panel"] as JObject, out error)
                || !HasValidReferenceResolution(document?["panel"] as JObject, out error))
                return false;

            var existingPanel = AssetDatabase.LoadMainAssetAtPath(panelPath);
            if (existingPanel != null && !panelType.IsInstanceOfType(existingPanel))
            {
                error = $"'{panelPath}' is not a PanelSettings asset.";
                return false;
            }

            var documentType = ComponentTypeResolver.Resolve("UIDocument");
            if (documentType == null)
            {
                error = "UI Toolkit UIDocument is unavailable in this Editor.";
                return false;
            }
            var visualTreeType = FindType("UnityEngine.UIElements.VisualTreeAsset");
            if (visualTreeType == null
                || !HasWritableObjectProperty(documentType, "panelSettings", panelType)
                || !HasWritableObjectProperty(documentType, "visualTreeAsset", visualTreeType))
            {
                error = "UI Toolkit UIDocument bindings are unavailable in this Editor.";
                return false;
            }

            preflight = new ApplyPreflight { PanelType = panelType, DocumentType = documentType };
            return true;
        }

        private static bool CanSetPanelRenderMode(Type panelType, JObject panelConfig, out string error)
        {
            error = null;
            if (!UiToolkitFixer.TryGetPanelRenderMode(panelConfig, out var renderMode, out error)) return false;
            var property = panelType.GetProperty("renderMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null && renderMode == "ScreenSpaceOverlay") return true;
            if (property == null || !property.CanWrite || !property.PropertyType.IsEnum || !Enum.IsDefined(property.PropertyType, renderMode))
            {
                error = $"{panelType.Name}.renderMode is unavailable.";
                return false;
            }
            return true;
        }

        private static bool HasValidReferenceResolution(JObject panelConfig, out string error)
        {
            error = null;
            var resolution = panelConfig?["reference_resolution"] as JArray;
            if (resolution == null || resolution.Count != 2) return true;
            if (resolution[0]?.Type == JTokenType.Integer && resolution[1]?.Type == JTokenType.Integer) return true;
            error = "panel.reference_resolution must be a two-integer array.";
            return false;
        }

        private static bool HasWritableObjectProperty(Type targetType, string name, Type valueType)
        {
            var property = targetType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            return property != null && property.CanWrite && property.PropertyType.IsAssignableFrom(valueType);
        }

        private static void ThrowIfFailureInjected(string stage)
        {
            var exception = FailureInjectionForTests?.Invoke(stage);
            if (exception != null) throw exception;
        }

        private static ApplyResult FailAfterMutation(ApplyResult result, string error, bool upsert, bool mutationsStarted, IList<string> createdAssets, GameObject createdRoot)
        {
            result.Errors.Add(string.IsNullOrEmpty(error) ? "failed to emit UI Toolkit document." : error);
            if (!mutationsStarted) return result;
            if (upsert)
            {
                result.UpsertMayBePartial = true;
                return result;
            }

            result.RollbackAttempted = true;
            if (createdRoot != null)
            {
                try
                {
                    var rootName = createdRoot.name;
                    UnityEngine.Object.DestroyImmediate(createdRoot);
                    result.RolledBackArtifacts.Add("scene:" + rootName);
                }
                catch (Exception ex)
                {
                    result.RollbackErrors.Add("failed to remove generated UIDocument GameObject: " + ex.Message);
                }
            }
            for (var i = createdAssets.Count - 1; i >= 0; i--)
            {
                var assetPath = createdAssets[i];
                try
                {
                    var fullPath = ToFullPath(assetPath);
                    var metaPath = fullPath + ".meta";
                    if (!File.Exists(fullPath) && !File.Exists(metaPath) && AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
                        continue;
                    if (AssetDatabase.DeleteAsset(assetPath))
                    {
                        result.RolledBackArtifacts.Add(assetPath);
                        continue;
                    }
                    if (File.Exists(fullPath)) File.Delete(fullPath);
                    if (File.Exists(metaPath)) File.Delete(metaPath);
                    result.RolledBackArtifacts.Add(assetPath);
                }
                catch (Exception ex)
                {
                    result.RollbackErrors.Add($"failed to remove generated asset '{assetPath}': {ex.Message}");
                }
            }
            try { AssetDatabase.SaveAssets(); }
            catch (Exception ex) { result.RollbackErrors.Add("failed to save UI Toolkit rollback: " + ex.Message); }
            return result;
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

        private static bool WriteAssets(string uxmlPath, string ussPath, string uxml, string uss, bool trackCreatedAssets, ICollection<string> createdAssets, out string error)
        {
            error = null;
            try
            {
                var directory = Path.GetDirectoryName(ToFullPath(uxmlPath));
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                var ussWasPresent = File.Exists(ToFullPath(ussPath));
                if (trackCreatedAssets && !ussWasPresent) createdAssets.Add(ussPath);
                File.WriteAllText(ToFullPath(ussPath), uss, new UTF8Encoding(false));
                var uxmlWasPresent = File.Exists(ToFullPath(uxmlPath));
                if (trackCreatedAssets && !uxmlWasPresent) createdAssets.Add(uxmlPath);
                File.WriteAllText(ToFullPath(uxmlPath), uxml, new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                error = $"failed to write generated UI Toolkit assets: {ex.Message}";
                return false;
            }
        }

        private static ScriptableObject CreateOrUpdatePanelSettings(Type panelType, string panelPath, JObject panelConfig, string renderMode, out string error, out bool created)
        {
            error = null;
            created = false;

            var panel = AssetDatabase.LoadAssetAtPath<ScriptableObject>(panelPath);
            try
            {
                if (panel == null)
                {
                    panel = ScriptableObject.CreateInstance(panelType);
                    AssetDatabase.CreateAsset(panel, panelPath);
                    created = true;
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

        private static GameObject CreateOrUpdateDocument(Type documentType, string rootName, Transform parent, bool upsert, ScriptableObject panelSettings, UnityEngine.Object visualTree, out string error, out bool created, out GameObject createdRoot)
        {
            error = null;
            created = false;
            createdRoot = null;

            try
            {
                var root = upsert ? FindSceneObject(rootName) : null;
                if (root == null)
                {
                    root = new GameObject(rootName);
                    createdRoot = root;
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
