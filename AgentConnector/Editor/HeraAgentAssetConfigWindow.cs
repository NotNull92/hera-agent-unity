using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace HeraAgent.Editor
{
    /// <summary>
    /// Hera Agent Unity — Hera Settings Editor Window (UIToolkit).
    /// A pure-Unity EditorWindow with no third-party dependencies.
    /// Shares asset-config.json with the hera-agent-unity CLI.
    ///
    /// Menu: Tools / Hera Agent Unity / Hera Settings
    /// </summary>
    public partial class HeraAgentAssetConfigWindow : EditorWindow
    {
        // ═══════════════════════════════════════════════════════════
        //  PALETTE  —  refined for depth and hierarchy
        // ═══════════════════════════════════════════════════════════

        // Old Money palette — heritage, restraint, premium
        private static readonly Color ColorGold        = Hex("#C9A227"); // Antique Gold (primary accent)
        private static readonly Color ColorGoldDark    = Hex("#9C7E1E"); // darker gold (hover/pressed)
        private static readonly Color ColorGoldGlow    = Hex("#C9A227"); // alias of primary
        private static readonly Color ColorAmber       = Hex("#9C7E1E"); // darker gold (secondary emphasis)
        private static readonly Color ColorAmberDark   = Hex("#7A6418"); // deepest gold
        private static readonly Color ColorError       = Hex("#8B3A3A"); // deep burgundy
        private static readonly Color ColorMuted       = Hex("#8B8178"); // Warm Gray
        private static readonly Color ColorDarkMuted   = Hex("#5C544B"); // dark warm gray
        private static readonly Color ColorBgCard      = Hex("#22201A"); // warm dark
        private static readonly Color ColorBgCardHover = Hex("#2A2820"); // warm dark hover
        private static readonly Color ColorBgCardSelected = Hex("#2A2418"); // gold-warm tint
        private static readonly Color ColorBorder      = Hex("#3A352E"); // warm border
        private static readonly Color ColorBorderSelected = Hex("#C9A227"); // accent
        private static readonly Color ColorBgDark      = Hex("#1A1814"); // warm dark
        private static readonly Color ColorBgDarker    = Hex("#13110D"); // warm darker
        private static readonly Color ColorTextPrimary = Hex("#F5F1E8"); // Cream
        private static readonly Color ColorTextSecondary = Hex("#B0A89B"); // warm light gray
        private static readonly Color ColorSuccessBg   = new Color(0.20f, 0.26f, 0.12f); // dark sage
        private static readonly Color ColorSuccessText = new Color(0.55f, 0.71f, 0.36f); // muted sage
        private static readonly Color ColorMissingBg   = new Color(0.13f, 0.12f, 0.10f); // warm dark
        private static readonly Color ColorMissingText = new Color(0.45f, 0.42f, 0.38f); // warm muted

        private static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return Color.gray;
        }

        // ═══════════════════════════════════════════════════════════
        //  DATA MODEL
        // ═══════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════
        //  STATE
        // ═══════════════════════════════════════════════════════════

        private AssetConfig _config;
        private string _configPath;
        private bool _isDirty;
        private string _selectedAssetId;
        private string _activeCategoryFilter;
        private string _searchQuery = "";

        private readonly Dictionary<string, VisualElement> _assetCards = new Dictionary<string, VisualElement>();
        private readonly Dictionary<string, VisualElement> _assetDetailPanels = new Dictionary<string, VisualElement>();
        private readonly Dictionary<string, VisualElement> _categoryHeaders = new Dictionary<string, VisualElement>();
        private readonly Dictionary<string, bool> _categoryExpanded = new Dictionary<string, bool>();
        private readonly Dictionary<string, Label> _categoryCountLabels = new Dictionary<string, Label>();

        private static readonly Dictionary<string, string> CategoryLabels = new Dictionary<string, string>
        {
            { "inspector",     "Inspector" },
            { "validation",    "Validation" },
            { "serialization", "Serialization" },
            { "animation",     "Animation / Effects" },
        };

        private static readonly string[] CategoryOrder =
        {
            "inspector", "validation", "serialization", "animation"
        };

        // ═══════════════════════════════════════════════════════════
        //  UI REFERENCES
        // ═══════════════════════════════════════════════════════════

        private VisualElement _root;
        private ToolbarSearchField _searchField;
        private VisualElement _contentContainer;
        private VisualElement _categoryPillsContainer;
        private VisualElement _loadingOverlay;
        private Label _loadingLabel;
        private Label _statusLabel;
        private Label _dirtyIndicator;
        private VisualElement _emptyStateContainer;
        private Label _cscPathLabel;
        private Label _dotnetPathLabel;
        private Label _gameFeelDotweenLabel;

        // ═══════════════════════════════════════════════════════════
        //  ENTRY POINTS
        // ═══════════════════════════════════════════════════════════

        [MenuItem("HeraAgent/Hera Settings")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<HeraAgentAssetConfigWindow>(
                title: "Hera Settings",
                focus: true
            );
            wnd.minSize = new Vector2(520, 480);
            wnd.maxSize = new Vector2(800, 1200);
            wnd.position = new Rect(wnd.position.x, wnd.position.y, 800, 720);
        }

        [MenuItem("HeraAgent/Hera Settings", true)]
        private static bool ValidateShowWindow()
        {
            return IsHeraAgentInstalled();
        }

        private static bool IsHeraAgentInstalled()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var configDir = Path.Combine(home, ".hera-agent-unity");
            return Directory.Exists(configDir);
        }

        // ═══════════════════════════════════════════════════════════
        //  CREATE GUI  —  main entry
        // ═══════════════════════════════════════════════════════════

        public void CreateGUI()
        {
            _root = rootVisualElement;
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.backgroundColor = ColorBgDark;

            if (!IsHeraAgentInstalled())
            {
                BuildNotInstalledUI(_root);
                return;
            }

            _configPath = GetConfigPath();
            LoadConfig();
            EnsureDefaults();
            InitializeCategoryStates();

            BuildToolbar();
            BuildUltraHeraSection();
            BuildGameFeelModeSection();
            BuildGameFeelUiModeSection();
            BuildCompilerPathSection();
            BuildCategoryPills();
            RefreshCategoryPills();
            BuildContentArea();
            BuildLoadingOverlay();
            BuildStatusBar();
            RegisterKeyboardShortcuts();

            RefreshUI();
        }

        // ═══════════════════════════════════════════════════════════
        //  TOOLBAR
        // ═══════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════
        //  CATEGORY PILLS  —  filter tabs
        // ═══════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════
        //  CONTENT AREA  —  scrollable sections
        // ═══════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════
        //  MAIN UI REFRESH  —  rebuild from state
        // ═══════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════
        //  CATEGORY SECTION  —  collapsible with bulk actions
        // ═══════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════
        //  ASSET CARD  —  the star of the show
        // ═══════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════
        //  SELECTION  —  expand/collapse detail panels
        // ═══════════════════════════════════════════════════════════

        private void SelectAsset(string id)
        {
            // Collapse previous
            if (!string.IsNullOrEmpty(_selectedAssetId) &&
                _assetCards.TryGetValue(_selectedAssetId, out var prevCard) &&
                _assetDetailPanels.TryGetValue(_selectedAssetId, out var prevDetail))
            {
                prevCard.style.backgroundColor = ColorBgCard;
                prevCard.style.borderTopColor = ColorBorder;
                prevCard.style.borderRightColor = ColorBorder;
                prevCard.style.borderBottomColor = ColorBorder;
                prevDetail.style.display = DisplayStyle.None;
            }

            _selectedAssetId = id;

            // Expand new
            if (_assetCards.TryGetValue(id, out var newCard) &&
                _assetDetailPanels.TryGetValue(id, out var newDetail))
            {
                newCard.style.backgroundColor = ColorBgCardSelected;
                newCard.style.borderTopColor = ColorBorderSelected;
                newCard.style.borderRightColor = ColorBorderSelected;
                newCard.style.borderBottomColor = ColorBorderSelected;
                newDetail.style.display = DisplayStyle.Flex;
            }
        }

        private void DeselectAsset()
        {
            if (string.IsNullOrEmpty(_selectedAssetId)) return;

            if (_assetCards.TryGetValue(_selectedAssetId, out var card) &&
                _assetDetailPanels.TryGetValue(_selectedAssetId, out var detail))
            {
                card.style.backgroundColor = ColorBgCard;
                card.style.borderTopColor = ColorBorder;
                card.style.borderRightColor = ColorBorder;
                card.style.borderBottomColor = ColorBorder;
                detail.style.display = DisplayStyle.None;
            }

            _selectedAssetId = null;
        }

        // ═══════════════════════════════════════════════════════════
        //  STATUS PILL
        // ═══════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════
        //  BULK ACTIONS
        // ═══════════════════════════════════════════════════════════

        private void EnableAllInCategory(string category)
        {
            var changed = false;
            foreach (var asset in _config.assets.Where(a => a.category == category))
            {
                if (!asset.enabled)
                {
                    asset.enabled = true;
                    changed = true;
                }
            }
            if (changed)
            {
                _isDirty = true;
                RefreshCardStates();
                UpdateStatusBar();
            }
        }

        private void DisableAllInCategory(string category)
        {
            var changed = false;
            foreach (var asset in _config.assets.Where(a => a.category == category))
            {
                if (asset.enabled)
                {
                    asset.enabled = false;
                    changed = true;
                }
            }
            if (changed)
            {
                _isDirty = true;
                RefreshCardStates();
                UpdateStatusBar();
            }
        }

        private void RefreshCardStates()
        {
            foreach (var asset in _config.assets)
            {
                if (!_assetCards.TryGetValue(asset.id, out var card)) continue;
                card.style.borderLeftColor = asset.enabled ? ColorGold : new Color(0.18f, 0.18f, 0.18f);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  EMPTY STATE
        // ═══════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════
        //  LOADING OVERLAY
        // ═══════════════════════════════════════════════════════════

        private void ShowLoading(string message)
        {
            if (_loadingLabel != null)
                _loadingLabel.text = message;
            if (_loadingOverlay != null)
                _loadingOverlay.style.display = DisplayStyle.Flex;
        }

        private void HideLoading()
        {
            if (_loadingOverlay != null)
                _loadingOverlay.style.display = DisplayStyle.None;
        }

        // ═══════════════════════════════════════════════════════════
        //  STATUS BAR
        // ═══════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════
        //  KEYBOARD SHORTCUTS
        // ═══════════════════════════════════════════════════════════

        private void RegisterKeyboardShortcuts()
        {
            _root.RegisterCallback<KeyDownEvent>(evt =>
            {
                switch (evt.keyCode)
                {
                    case KeyCode.S when evt.ctrlKey:
                        evt.StopPropagation();
                        SaveConfig();
                        break;
                    case KeyCode.F when evt.ctrlKey:
                        evt.StopPropagation();
                        _searchField?.Focus();
                        break;
                    case KeyCode.Escape when !string.IsNullOrEmpty(_selectedAssetId):
                        evt.StopPropagation();
                        DeselectAsset();
                        break;
                }
            });
        }

        // ═══════════════════════════════════════════════════════════
        //  NOT INSTALLED  —  fallback UI
        // ═══════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════
        //  PERSISTENCE
        // ═══════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════
        //  DETECTION  —  3-tier heuristic with progress overlay
        // ═══════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════
        //  STYLE HELPERS
        // ═══════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════
        //  COMPILER PATH CONFIGURATION
        // ═══════════════════════════════════════════════════════════

        private void AutoDetectCscPath()
        {
            var path = FindValidCscPath();
            if (!string.IsNullOrEmpty(path))
            {
                _config.defaultCscPath = path;
                _isDirty = true;
                SaveConfig();
                UpdateCompilerPathLabels();
                Debug.Log($"[Hera] CSC path auto-detected and saved: {path}");
            }
            else
            {
                EditorUtility.DisplayDialog("Auto-detect Failed",
                    "Could not find a valid CSC compiler on this system.\n\n"
                    + "Please install the .NET SDK or set the path manually.",
                    "OK");
            }
        }

        private void AutoDetectDotnetPath()
        {
            var path = FindValidDotnetPath();
            if (!string.IsNullOrEmpty(path))
            {
                _config.defaultDotnetPath = path;
                _isDirty = true;
                SaveConfig();
                UpdateCompilerPathLabels();
                Debug.Log($"[Hera] dotnet path auto-detected and saved: {path}");
            }
            else
            {
                EditorUtility.DisplayDialog("Auto-detect Failed",
                    "Could not find the dotnet runtime on this system.\n\n"
                    + "Please install .NET or set the path manually.",
                    "OK");
            }
        }

        private void ClearCscPath()
        {
            _config.defaultCscPath = null;
            _isDirty = true;
            SaveConfig();
            UpdateCompilerPathLabels();
        }

        private void ClearDotnetPath()
        {
            _config.defaultDotnetPath = null;
            _isDirty = true;
            SaveConfig();
            UpdateCompilerPathLabels();
        }

        private static string s_CachedCscPath;
        private static string s_CachedDotnetPath;

        /// <summary>
        /// Returns the minimum recommended .NET SDK version for the current Unity Editor.
        /// Unity 6 (6000.x) recommends .NET 8+; Unity 2022+ recommends .NET 6+.
        /// </summary>
        private static Version GetMinimumRecommendedSdkVersion()
        {
            var uv = Application.unityVersion ?? "";
            if (uv.StartsWith("6000.")) return new Version(8, 0);
            if (uv.StartsWith("2022.") || uv.StartsWith("2023.")) return new Version(6, 0);
            return new Version(0, 0);
        }

        /// <summary>
        /// Extracts a System.Version from a .NET SDK directory name (e.g. "10.0.203").
        /// </summary>
        private static Version ExtractSdkVersionFromPath(string sdkDirectoryPath)
        {
            var dirName = Path.GetFileName(sdkDirectoryPath);
            if (Version.TryParse(dirName, out var version))
                return version;
            return new Version(0, 0);
        }

        private static bool IsValidCscPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                return true;

            var dir = Path.GetDirectoryName(path);
            for (var current = dir; !string.IsNullOrEmpty(current); current = Directory.GetParent(current)?.FullName)
            {
                var dirName = Path.GetFileName(current);
                if (System.Text.RegularExpressions.Regex.IsMatch(dirName ?? "", @"^\d+\.\d+"))
                    return true;
            }

            if (dir == null)
                return false;

            if (path.Replace('\\', '/').IndexOf("/DotNetSdkRoslyn/", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            var requiredDeps = new[] { "System.Text.Encoding.CodePages.dll" };
            foreach (var dep in requiredDeps)
            {
                if (!File.Exists(Path.Combine(dir, dep)))
                    return false;
            }
            return true;
        }
    }
}
