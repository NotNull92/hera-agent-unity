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
    /// Hera Agent Pro — Hera Settings Editor Window (UIToolkit).
    /// A pure-Unity EditorWindow with no third-party dependencies.
    /// Shares asset-config.json with hera-agent-unity-pro.
    ///
    /// Menu: Tools / Hera Agent Pro / Hera Settings
    /// </summary>
    public class HeraAgentAssetConfigWindow : EditorWindow
    {
        // ═══════════════════════════════════════════════════════════
        //  PALETTE  —  refined for depth and hierarchy
        // ═══════════════════════════════════════════════════════════

        // Old Money palette — heritage, restraint, premium
        private static readonly Color ColorTeal        = Hex("#C9A227"); // Antique Gold (primary accent)
        private static readonly Color ColorTealDark    = Hex("#9C7E1E"); // darker gold (hover/pressed)
        private static readonly Color ColorTealGlow    = Hex("#C9A227"); // alias of primary
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

        [System.Serializable]
        private class AssetEntry
        {
            public string id;
            public string name;
            public bool enabled;
            public bool installed;
            public string category;
            public string description;
            public string doc_url;
            public string reference_path;
        }

        [System.Serializable]
        private class AssetConfig
        {
            public string version = "1.0.0";
            public List<AssetEntry> assets = new List<AssetEntry>();
            public string defaultCscPath;
            public string defaultDotnetPath;
        }

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

        // ═══════════════════════════════════════════════════════════
        //  ENTRY POINTS
        // ═══════════════════════════════════════════════════════════

        [MenuItem("Tools/Hera Agent Pro/Hera Settings")]
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

        [MenuItem("Tools/Hera Agent Pro/Hera Settings", true)]
        private static bool ValidateShowWindow()
        {
            return IsHeraAgentInstalled();
        }

        private static bool IsHeraAgentInstalled()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var configDir = Path.Combine(home, ".hera-agent-unity-unity-pro");
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

        private void BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.style.height = 28;
            toolbar.style.backgroundColor = ColorBgDarker;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = ColorBorder;

            // Title icon + text
            var titleContainer = new VisualElement();
            titleContainer.style.flexDirection = FlexDirection.Row;
            titleContainer.style.alignItems = Align.Center;
            titleContainer.style.marginLeft = 4;

            var iconLbl = new Label("⚙");
            iconLbl.style.fontSize = 14;
            iconLbl.style.marginRight = 6;
            titleContainer.Add(iconLbl);

            var titleLbl = new Label("Asset Catalog");
            titleLbl.style.fontSize = 13;
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLbl.style.color = ColorTeal;
            titleContainer.Add(titleLbl);

            toolbar.Add(titleContainer);

            // Flexible spacer
            var spacer = new ToolbarSpacer { flex = true };
            toolbar.Add(spacer);

            // Search field
            _searchField = new ToolbarSearchField();
            _searchField.style.width = 180;
            _searchField.style.marginRight = 8;
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchQuery = evt.newValue;
                RefreshUI();
            });
            toolbar.Add(_searchField);

            // Detect button
            var detectBtn = new ToolbarButton(DetectInstalledAssets) { text = "🔄 Detect" };
            detectBtn.style.width = 80;
            toolbar.Add(detectBtn);

            // Save button
            var saveBtn = new ToolbarButton(SaveConfig) { text = "💾 Save" };
            saveBtn.style.width = 70;
            toolbar.Add(saveBtn);

            _root.Add(toolbar);
        }

        // ═══════════════════════════════════════════════════════════
        //  CATEGORY PILLS  —  filter tabs
        // ═══════════════════════════════════════════════════════════

        private void BuildCategoryPills()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.paddingTop = 10;
            container.style.paddingBottom = 8;
            container.style.paddingLeft = 12;
            container.style.paddingRight = 12;
            container.style.borderBottomWidth = 1;
            container.style.borderBottomColor = ColorBorder;
            container.style.backgroundColor = ColorBgDark;
            container.style.minHeight = 40;

            _categoryPillsContainer = container;
            _root.Add(container);
        }

        private VisualElement CreateCategoryPill(string label, int count, bool active, Action onClick)
        {
            var pill = new VisualElement();
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems = Align.Center;
            pill.style.paddingTop = 4;
            pill.style.paddingBottom = 4;
            pill.style.paddingLeft = 10;
            pill.style.paddingRight = 10;
            pill.style.marginRight = 6;
            pill.style.borderTopLeftRadius = 12;
            pill.style.borderTopRightRadius = 12;
            pill.style.borderBottomLeftRadius = 12;
            pill.style.borderBottomRightRadius = 12;
            pill.style.minHeight = 26;

            if (active)
            {
                pill.style.backgroundColor = ColorTeal;
            }
            else
            {
                pill.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            }

            var nameLbl = new Label(label);
            nameLbl.style.fontSize = 11;
            nameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLbl.style.color = active ? ColorBgDarker : ColorTextSecondary;
            nameLbl.style.marginRight = 4;
            pill.Add(nameLbl);

            var countLbl = new Label(count.ToString());
            countLbl.style.fontSize = 10;
            countLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            countLbl.style.color = active ? ColorTealDark : ColorDarkMuted;
            countLbl.style.backgroundColor = active ? ColorBgDark : new Color(0.12f, 0.12f, 0.12f);
            countLbl.style.paddingLeft = 5;
            countLbl.style.paddingRight = 5;
            countLbl.style.paddingTop = 2;
            countLbl.style.paddingBottom = 2;
            countLbl.style.borderTopLeftRadius = 8;
            countLbl.style.borderTopRightRadius = 8;
            countLbl.style.borderBottomLeftRadius = 8;
            countLbl.style.borderBottomRightRadius = 8;
            pill.Add(countLbl);

            pill.RegisterCallback<ClickEvent>(_ => onClick?.Invoke());

            // Hover effect for inactive pills
            if (!active)
            {
                pill.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    pill.style.backgroundColor = new Color(0.28f, 0.28f, 0.28f);
                    nameLbl.style.color = ColorTextPrimary;
                });
                pill.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    pill.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
                    nameLbl.style.color = ColorTextSecondary;
                });
            }

            return pill;
        }

        private void RefreshCategoryPills()
        {
            if (_categoryPillsContainer == null) return;
            _categoryPillsContainer.Clear();

            var allCount = GetFilteredAssets().Count;
            _categoryPillsContainer.Add(CreateCategoryPill("All", allCount,
                string.IsNullOrEmpty(_activeCategoryFilter), () =>
            {
                _activeCategoryFilter = null;
                RefreshCategoryPills();
                RefreshUI();
            }));

            foreach (var catKey in CategoryOrder)
            {
                if (!CategoryLabels.TryGetValue(catKey, out var catLabel)) continue;
                var count = _config.assets.Count(a => a.category == catKey);
                var key = catKey;
                _categoryPillsContainer.Add(CreateCategoryPill(catLabel, count,
                    _activeCategoryFilter == key, () =>
                {
                    _activeCategoryFilter = key;
                    RefreshCategoryPills();
                    RefreshUI();
                }));
            }

            _categoryPillsContainer.MarkDirtyRepaint();
        }

        // ═══════════════════════════════════════════════════════════
        //  CONTENT AREA  —  scrollable sections
        // ═══════════════════════════════════════════════════════════

        private void BuildContentArea()
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.paddingLeft = 12;
            scroll.style.paddingRight = 12;
            scroll.style.paddingTop = 8;
            scroll.style.paddingBottom = 8;
            scroll.style.backgroundColor = ColorBgDark;

            _contentContainer = new VisualElement();
            scroll.Add(_contentContainer);

            _root.Add(scroll);
        }

        // ═══════════════════════════════════════════════════════════
        //  MAIN UI REFRESH  —  rebuild from state
        // ═══════════════════════════════════════════════════════════

        private void RefreshUI()
        {
            if (_contentContainer == null) return;
            _contentContainer.Clear();
            _assetCards.Clear();
            _assetDetailPanels.Clear();
            _categoryHeaders.Clear();
            _categoryCountLabels.Clear();

            var assets = GetFilteredAssets();

            if (assets.Count == 0)
            {
                BuildEmptyState();
                UpdateStatusBar();
                return;
            }

            var grouped = assets.GroupBy(a => a.category).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var catKey in CategoryOrder)
            {
                if (!grouped.TryGetValue(catKey, out var items)) continue;
                if (items.Count == 0) continue;

                _contentContainer.Add(BuildCategorySection(catKey, items));
            }

            UpdateStatusBar();
        }

        private List<AssetEntry> GetFilteredAssets()
        {
            var query = _searchQuery.Trim().ToLowerInvariant();
            var assets = _config.assets;

            if (!string.IsNullOrEmpty(_activeCategoryFilter))
            {
                assets = assets.Where(a => a.category == _activeCategoryFilter).ToList();
            }

            if (!string.IsNullOrEmpty(query))
            {
                assets = assets.Where(a =>
                    a.name.ToLowerInvariant().Contains(query) ||
                    (a.description ?? "").ToLowerInvariant().Contains(query) ||
                    a.category.ToLowerInvariant().Contains(query)
                ).ToList();
            }

            return assets;
        }

        // ═══════════════════════════════════════════════════════════
        //  CATEGORY SECTION  —  collapsible with bulk actions
        // ═══════════════════════════════════════════════════════════

        private VisualElement BuildCategorySection(string catKey, List<AssetEntry> items)
        {
            var section = new VisualElement();
            section.style.marginBottom = 20;

            // ── Header row ──
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 4;
            header.style.paddingBottom = 6;
            _categoryHeaders[catKey] = header;

            bool expanded = _categoryExpanded.GetValueOrDefault(catKey, true);

            var arrow = new Label(expanded ? "\u25bc" : "\u25b6");
            arrow.style.fontSize = 10;
            arrow.style.color = ColorAmber;
            arrow.style.marginRight = 8;
            arrow.style.width = 14;
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.Add(arrow);

            var nameLbl = new Label(CategoryLabels.GetValueOrDefault(catKey, catKey));
            nameLbl.style.fontSize = 13;
            nameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLbl.style.color = ColorAmber;
            nameLbl.style.flexGrow = 1;
            header.Add(nameLbl);

            // Count badge
            var countPill = new Label(items.Count.ToString());
            countPill.style.fontSize = 10;
            countPill.style.unityFontStyleAndWeight = FontStyle.Bold;
            countPill.style.color = ColorBgDarker;
            countPill.style.backgroundColor = ColorAmber;
            countPill.style.paddingTop = 2;
            countPill.style.paddingBottom = 2;
            countPill.style.paddingLeft = 8;
            countPill.style.paddingRight = 8;
            countPill.style.borderTopLeftRadius = 10;
            countPill.style.borderTopRightRadius = 10;
            countPill.style.borderBottomLeftRadius = 10;
            countPill.style.borderBottomRightRadius = 10;
            header.Add(countPill);
            _categoryCountLabels[catKey] = countPill;

            section.Add(header);

            // ── Bulk actions row ──
            var bulkRow = new VisualElement();
            bulkRow.style.flexDirection = FlexDirection.Row;
            bulkRow.style.marginBottom = 8;
            bulkRow.style.paddingLeft = 22;
            bulkRow.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;

            var enableAllBtn = new Button(() => EnableAllInCategory(catKey))
            {
                text = "Enable All",
                tooltip = "Enable all assets in this category"
            };
            StyleSmallButton(enableAllBtn, ColorTeal);
            enableAllBtn.style.marginRight = 6;
            bulkRow.Add(enableAllBtn);

            var disableAllBtn = new Button(() => DisableAllInCategory(catKey))
            {
                text = "Disable All",
                tooltip = "Disable all assets in this category"
            };
            StyleSmallButton(disableAllBtn, ColorDarkMuted);
            bulkRow.Add(disableAllBtn);

            section.Add(bulkRow);

            // ── Content ──
            var content = new VisualElement();
            content.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;

            foreach (var asset in items)
            {
                content.Add(BuildAssetCard(asset));
            }

            section.Add(content);

            // Toggle expand/collapse
            header.RegisterCallback<ClickEvent>(_ =>
            {
                expanded = !expanded;
                _categoryExpanded[catKey] = expanded;
                arrow.text = expanded ? "\u25bc" : "\u25b6";
                content.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                bulkRow.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            });

            return section;
        }

        // ═══════════════════════════════════════════════════════════
        //  ASSET CARD  —  the star of the show
        // ═══════════════════════════════════════════════════════════

        private VisualElement BuildAssetCard(AssetEntry asset)
        {
            var isSelected = _selectedAssetId == asset.id;

            var card = new VisualElement();
            card.style.backgroundColor = isSelected ? ColorBgCardSelected : ColorBgCard;
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = asset.enabled ? ColorTeal : new Color(0.18f, 0.18f, 0.18f);
            card.style.borderTopWidth = 1;
            card.style.borderTopColor = isSelected ? ColorBorderSelected : ColorBorder;
            card.style.borderRightWidth = 1;
            card.style.borderRightColor = isSelected ? ColorBorderSelected : ColorBorder;
            card.style.borderBottomWidth = 1;
            card.style.borderBottomColor = isSelected ? ColorBorderSelected : ColorBorder;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.marginBottom = 8;

            _assetCards[asset.id] = card;

            // Hover effects
            card.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (_selectedAssetId != asset.id)
                    card.style.backgroundColor = ColorBgCardHover;
            });
            card.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                card.style.backgroundColor = _selectedAssetId == asset.id ? ColorBgCardSelected : ColorBgCard;
            });

            // Click to select / deselect
            card.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Toggle) return; // Don't select when toggling
                if (_selectedAssetId == asset.id)
                    DeselectAsset();
                else
                    SelectAsset(asset.id);
            });

            // ── Top row: Toggle + Name + Status Pill ──
            var topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.alignItems = Align.Center;

            var toggle = new Toggle { value = asset.enabled, tooltip = "Enable this asset" };
            toggle.style.marginRight = 10;
            toggle.RegisterValueChangedCallback(evt =>
            {
                asset.enabled = evt.newValue;
                _isDirty = true;
                card.style.borderLeftColor = asset.enabled ? ColorTeal : new Color(0.18f, 0.18f, 0.18f);
                UpdateStatusBar();
            });
            topRow.Add(toggle);

            var nameLbl = new Label(asset.name);
            nameLbl.style.fontSize = 12;
            nameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLbl.style.color = ColorTextPrimary;
            nameLbl.style.flexGrow = 1;
            topRow.Add(nameLbl);

            topRow.Add(BuildStatusPill(asset.installed));

            card.Add(topRow);

            // ── Description ──
            if (!string.IsNullOrEmpty(asset.description))
            {
                var descLbl = new Label(asset.description);
                descLbl.style.fontSize = 10;
                descLbl.style.color = ColorTextSecondary;
                descLbl.style.whiteSpace = WhiteSpace.Normal;
                descLbl.style.marginTop = 6;
                descLbl.style.paddingLeft = 28;
                descLbl.style.unityFontStyleAndWeight = FontStyle.Italic;
                card.Add(descLbl);
            }

            // ── Detail panel (hidden by default) ──
            var detailPanel = new VisualElement();
            detailPanel.style.display = isSelected ? DisplayStyle.Flex : DisplayStyle.None;
            detailPanel.style.marginTop = 10;
            detailPanel.style.paddingTop = 10;
            detailPanel.style.paddingLeft = 28;
            detailPanel.style.borderTopWidth = 1;
            detailPanel.style.borderTopColor = ColorBorder;

            // Category tag
            var tagRow = new VisualElement();
            tagRow.style.flexDirection = FlexDirection.Row;
            tagRow.style.marginBottom = 8;

            var catTag = new Label(CategoryLabels.GetValueOrDefault(asset.category, asset.category));
            catTag.style.fontSize = 9;
            catTag.style.unityFontStyleAndWeight = FontStyle.Bold;
            catTag.style.color = ColorAmber;
            catTag.style.backgroundColor = new Color(0.15f, 0.12f, 0.05f);
            catTag.style.paddingLeft = 6;
            catTag.style.paddingRight = 6;
            catTag.style.paddingTop = 2;
            catTag.style.paddingBottom = 2;
            catTag.style.borderTopLeftRadius = 4;
            catTag.style.borderTopRightRadius = 4;
            catTag.style.borderBottomLeftRadius = 4;
            catTag.style.borderBottomRightRadius = 4;
            tagRow.Add(catTag);

            detailPanel.Add(tagRow);

            // ID
            var idLbl = new Label($"ID: {asset.id}");
            idLbl.style.fontSize = 9;
            idLbl.style.color = ColorDarkMuted;
            idLbl.style.marginBottom = 4;
            detailPanel.Add(idLbl);

            // Doc URL button
            if (!string.IsNullOrEmpty(asset.doc_url))
            {
                var docBtn = new Button(() => Application.OpenURL(asset.doc_url))
                {
                    text = "📖 Open Documentation",
                    tooltip = asset.doc_url
                };
                StyleLinkButton(docBtn);
                docBtn.style.marginBottom = 6;
                detailPanel.Add(docBtn);
            }

            // Reference path
            var refRow = new VisualElement();
            refRow.style.flexDirection = FlexDirection.Row;
            refRow.style.alignItems = Align.Center;
            refRow.style.marginBottom = 2;

            var refIcon = new Label("📄");
            refIcon.style.fontSize = 10;
            refIcon.style.marginRight = 4;
            refRow.Add(refIcon);

            var refLbl = new Label($"Ref: {asset.reference_path}");
            refLbl.style.fontSize = 9;
            refLbl.style.color = ColorMuted;
            refRow.Add(refLbl);

            detailPanel.Add(refRow);

            // Config path
            var cfgRow = new VisualElement();
            cfgRow.style.flexDirection = FlexDirection.Row;
            cfgRow.style.alignItems = Align.Center;

            var cfgIcon = new Label("⚙️");
            cfgIcon.style.fontSize = 10;
            cfgIcon.style.marginRight = 4;
            cfgRow.Add(cfgIcon);

            var cfgLbl = new Label($"Config: {_configPath}");
            cfgLbl.style.fontSize = 9;
            cfgLbl.style.color = ColorMuted;
            cfgRow.Add(cfgLbl);

            detailPanel.Add(cfgRow);

            card.Add(detailPanel);
            _assetDetailPanels[asset.id] = detailPanel;

            return card;
        }

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

        private VisualElement BuildStatusPill(bool installed)
        {
            var pill = new VisualElement();
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems = Align.Center;
            pill.style.paddingTop = 3;
            pill.style.paddingBottom = 3;
            pill.style.paddingLeft = 8;
            pill.style.paddingRight = 8;
            pill.style.borderTopLeftRadius = 10;
            pill.style.borderTopRightRadius = 10;
            pill.style.borderBottomLeftRadius = 10;
            pill.style.borderBottomRightRadius = 10;

            if (installed)
            {
                pill.style.backgroundColor = ColorSuccessBg;

                var dot = new Label("\u25cf");
                dot.style.fontSize = 8;
                dot.style.color = ColorSuccessText;
                dot.style.marginRight = 4;
                pill.Add(dot);

                var txt = new Label("Installed");
                txt.style.fontSize = 9;
                txt.style.unityFontStyleAndWeight = FontStyle.Bold;
                txt.style.color = ColorSuccessText;
                pill.Add(txt);
            }
            else
            {
                pill.style.backgroundColor = ColorMissingBg;

                var dot = new Label("\u25cb");
                dot.style.fontSize = 8;
                dot.style.color = ColorMissingText;
                dot.style.marginRight = 4;
                pill.Add(dot);

                var txt = new Label("Missing");
                txt.style.fontSize = 9;
                txt.style.unityFontStyleAndWeight = FontStyle.Bold;
                txt.style.color = ColorMissingText;
                pill.Add(txt);
            }

            return pill;
        }

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
                card.style.borderLeftColor = asset.enabled ? ColorTeal : new Color(0.18f, 0.18f, 0.18f);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  EMPTY STATE
        // ═══════════════════════════════════════════════════════════

        private void BuildEmptyState()
        {
            var box = new VisualElement();
            box.style.flexGrow = 1;
            box.style.justifyContent = Justify.Center;
            box.style.alignItems = Align.Center;
            box.style.marginTop = 40;

            var iconLbl = new Label("🔍");
            iconLbl.style.fontSize = 32;
            iconLbl.style.marginBottom = 12;
            box.Add(iconLbl);

            var titleLbl = new Label("No assets match");
            titleLbl.style.fontSize = 14;
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLbl.style.color = ColorTextPrimary;
            titleLbl.style.marginBottom = 8;
            box.Add(titleLbl);

            var descLbl = new Label("No entries match the current query.\nRefine your search.");
            descLbl.style.fontSize = 11;
            descLbl.style.color = ColorMuted;
            descLbl.style.whiteSpace = WhiteSpace.Normal;
            descLbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            box.Add(descLbl);

            if (!string.IsNullOrEmpty(_searchQuery))
            {
                var clearBtn = new Button(() =>
                {
                    _searchField.value = "";
                    _searchQuery = "";
                    RefreshUI();
                })
                { text = "Clear Search" };
                StyleSmallButton(clearBtn, ColorTeal);
                clearBtn.style.marginTop = 12;
                box.Add(clearBtn);
            }

            _contentContainer.Add(box);
            _emptyStateContainer = box;
        }

        // ═══════════════════════════════════════════════════════════
        //  LOADING OVERLAY
        // ═══════════════════════════════════════════════════════════

        private void BuildLoadingOverlay()
        {
            _loadingOverlay = new VisualElement();
            _loadingOverlay.style.position = Position.Absolute;
            _loadingOverlay.style.left = 0;
            _loadingOverlay.style.top = 0;
            _loadingOverlay.style.right = 0;
            _loadingOverlay.style.bottom = 0;
            _loadingOverlay.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            _loadingOverlay.style.justifyContent = Justify.Center;
            _loadingOverlay.style.alignItems = Align.Center;
            _loadingOverlay.style.display = DisplayStyle.None;
            _loadingOverlay.pickingMode = PickingMode.Position;

            var spinnerBox = new VisualElement();
            spinnerBox.style.alignItems = Align.Center;

            var spinner = new Label("❖");
            spinner.style.fontSize = 36;
            spinner.style.marginBottom = 16;
            spinnerBox.Add(spinner);

            _loadingLabel = new Label("Surveying assets...");
            _loadingLabel.style.fontSize = 13;
            _loadingLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _loadingLabel.style.color = ColorTeal;
            spinnerBox.Add(_loadingLabel);

            var subLbl = new Label("This may take a moment.");
            subLbl.style.fontSize = 10;
            subLbl.style.color = ColorMuted;
            subLbl.style.marginTop = 8;
            spinnerBox.Add(subLbl);

            _loadingOverlay.Add(spinnerBox);
            _root.Add(_loadingOverlay);
        }

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

        private void BuildStatusBar()
        {
            var statusBar = new VisualElement();
            statusBar.style.flexDirection = FlexDirection.Row;
            statusBar.style.alignItems = Align.Center;
            statusBar.style.paddingTop = 6;
            statusBar.style.paddingBottom = 6;
            statusBar.style.paddingLeft = 12;
            statusBar.style.paddingRight = 12;
            statusBar.style.backgroundColor = ColorBgDarker;
            statusBar.style.borderTopWidth = 1;
            statusBar.style.borderTopColor = ColorBorder;
            statusBar.style.minHeight = 26;

            _statusLabel = new Label();
            _statusLabel.style.fontSize = 10;
            _statusLabel.style.color = ColorMuted;
            _statusLabel.style.flexGrow = 1;
            statusBar.Add(_statusLabel);

            _dirtyIndicator = new Label("● Modified");
            _dirtyIndicator.style.fontSize = 10;
            _dirtyIndicator.style.unityFontStyleAndWeight = FontStyle.Bold;
            _dirtyIndicator.style.color = ColorTeal;
            _dirtyIndicator.style.display = DisplayStyle.None;
            statusBar.Add(_dirtyIndicator);

            _root.Add(statusBar);
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            if (_statusLabel == null || _config == null) return;

            var total = _config.assets.Count;
            var enabled = _config.assets.Count(a => a.enabled);
            var installed = _config.assets.Count(a => a.installed);

            _statusLabel.text = $"{total} assets • {enabled} enabled • {installed} installed";

            if (_dirtyIndicator != null)
            {
                _dirtyIndicator.style.display = _isDirty ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // Update window title
            titleContent = new GUIContent(_isDirty ? "Hera Settings *" : "Hera Settings");
        }

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

        private void BuildNotInstalledUI(VisualElement root)
        {
            root.style.flexDirection = FlexDirection.Column;
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;
            root.style.backgroundColor = ColorBgDark;

            var iconLbl = new Label("❖");
            iconLbl.style.fontSize = 48;
            iconLbl.style.marginBottom = 16;
            root.Add(iconLbl);

            var titleLbl = new Label("Hera Agent Pro is not installed");
            titleLbl.style.fontSize = 16;
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLbl.style.color = ColorTextPrimary;
            titleLbl.style.marginBottom = 8;
            root.Add(titleLbl);

            var descLbl = new Label("Hera Agent Pro has not been commissioned yet.\nInstall the CLI and run it once to populate this catalog.");
            descLbl.style.fontSize = 11;
            descLbl.style.color = ColorMuted;
            descLbl.style.whiteSpace = WhiteSpace.Normal;
            descLbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            descLbl.style.marginBottom = 20;
            root.Add(descLbl);

            var cmdBox = new VisualElement();
            cmdBox.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            cmdBox.style.paddingTop = 10;
            cmdBox.style.paddingBottom = 10;
            cmdBox.style.paddingLeft = 16;
            cmdBox.style.paddingRight = 16;
            cmdBox.style.borderTopLeftRadius = 6;
            cmdBox.style.borderTopRightRadius = 6;
            cmdBox.style.borderBottomLeftRadius = 6;
            cmdBox.style.borderBottomRightRadius = 6;
            cmdBox.style.borderTopWidth = 1;
            cmdBox.style.borderBottomWidth = 1;
            cmdBox.style.borderLeftWidth = 1;
            cmdBox.style.borderRightWidth = 1;
            cmdBox.style.borderTopColor = ColorBorder;
            cmdBox.style.borderBottomColor = ColorBorder;
            cmdBox.style.borderLeftColor = ColorBorder;
            cmdBox.style.borderRightColor = ColorBorder;

            var cmdLbl = new Label("$ hera-agent-unity-pro setup");
            cmdLbl.style.fontSize = 11;
            cmdLbl.style.color = ColorAmber;
            cmdLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            cmdBox.Add(cmdLbl);

            root.Add(cmdBox);
        }

        // ═══════════════════════════════════════════════════════════
        //  PERSISTENCE
        // ═══════════════════════════════════════════════════════════

        private static string GetConfigPath()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".hera-agent-unity-unity-pro", "asset-config.json");
        }

        private void LoadConfig()
        {
            _isDirty = false;

            if (!File.Exists(_configPath))
            {
                _config = new AssetConfig();
                return;
            }

            try
            {
                var json = File.ReadAllText(_configPath);
                _config = JsonConvert.DeserializeObject<AssetConfig>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Hera] Failed to load asset-config.json: {ex.Message}");
                _config = new AssetConfig();
            }
        }

        private void SaveConfig()
        {
            if (_config == null) return;

            try
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText(_configPath, json);
                _isDirty = false;
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Hera] Failed to save asset-config.json: {ex.Message}");
                EditorUtility.DisplayDialog("Save Failed", $"Could not write config:\n{ex.Message}", "OK");
            }
        }

        private void EnsureDefaults()
        {
            if (_config == null)
                _config = new AssetConfig();

            var defaults = GetDefaultAssets();
            var existingIds = new HashSet<string>(_config.assets.Select(a => a.id));
            bool added = false;

            foreach (var def in defaults)
            {
                if (!existingIds.Contains(def.id))
                {
                    _config.assets.Add(def);
                    added = true;
                }
            }

            if (added)
                SaveConfig();
        }

        private void InitializeCategoryStates()
        {
            foreach (var cat in CategoryOrder)
                _categoryExpanded[cat] = true;
        }

        private static List<AssetEntry> GetDefaultAssets()
        {
            return new List<AssetEntry>
            {
                new AssetEntry
                {
                    id = "odin_inspector",
                    name = "Odin Inspector",
                    enabled = false,
                    installed = false,
                    category = "inspector",
                    description = "Powerful inspector extension. Prioritize Odin API when generating custom editor code.",
                    doc_url = "https://odininspector.com/documentation",
                    reference_path = "references/odin-inspector.md"
                },
                new AssetEntry
                {
                    id = "odin_validator",
                    name = "Odin Validator",
                    enabled = false,
                    installed = false,
                    category = "validation",
                    description = "Data validation system. Use Odin Validator for runtime and editor data integrity checks.",
                    doc_url = "https://odininspector.com/tutorials/odin-validator/getting-started-with-odin-validator",
                    reference_path = "references/odin-validator.md"
                },
                new AssetEntry
                {
                    id = "odin_serializer",
                    name = "Odin Serializer",
                    enabled = false,
                    installed = false,
                    category = "serialization",
                    description = "High-performance serialization. Replace Unity's default serializer with Odin Serializer for complex data structures.",
                    doc_url = "https://odininspector.com/tutorials/serialize-anything/odin-serializer-quick-start",
                    reference_path = "references/odin-serializer.md"
                },
                new AssetEntry
                {
                    id = "dotween",
                    name = "DOTween",
                    enabled = false,
                    installed = false,
                    category = "animation",
                    description = "Tweening and animation engine. Default to DOTween API for all tweening and timing implementations.",
                    doc_url = "https://dotween.demigiant.com/documentation.php",
                    reference_path = "references/dotween.md"
                },
                new AssetEntry
                {
                    id = "dotween_pro",
                    name = "DOTween Pro",
                    enabled = false,
                    installed = false,
                    category = "animation",
                    description = "Advanced tweening features including Visual Animation, Physics2D integration, and Audio tweening.",
                    doc_url = "https://dotween.demigiant.com/pro.php",
                    reference_path = "references/dotween-pro.md"
                }
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  DETECTION  —  3-tier heuristic with progress overlay
        // ═══════════════════════════════════════════════════════════

        private void DetectInstalledAssets()
        {
            ShowLoading("Surveying assets...");
            EditorApplication.delayCall += () => RunDetection();
        }

        private void RunDetection()
        {
            var projectPath = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            int detectedCount = 0;

            var rules = new (string id, string[] folders, string[] dlls, string[] assemblies)[]
            {
                ("odin_inspector",
                 new[] {
                     "Assets/Plugins/Sirenix",
                     "Assets/ThirdParty/Sirenix",
                     "Assets/Sirenix",
                     "Packages/com.sirenix.odin-inspector"
                 },
                 new[] {
                     "Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Attributes.dll",
                     "Assets/Plugins/Sirenix/Assemblies/NoEditor/Sirenix.OdinInspector.Attributes.dll",
                     "Assets/Plugins/Sirenix/Assemblies/NoEmitAndNoEditor/Sirenix.OdinInspector.Attributes.dll"
                 },
                 new[] { "Sirenix.OdinInspector.Attributes", "Sirenix.OdinInspector.Editor" }),

                ("odin_validator",
                 new[] {
                     "Assets/Plugins/Sirenix/Odin Validator",
                     "Assets/Plugins/Sirenix/Odin/Modules/Sirenix.OdinValidator",
                     "Assets/ThirdParty/Sirenix/Odin/Modules/Sirenix.OdinValidator",
                     "Packages/com.sirenix.odin-validator"
                 },
                 new[] {
                     "Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinValidator.dll",
                     "Assets/Plugins/Sirenix/Assemblies/NoEditor/Sirenix.OdinValidator.dll"
                 },
                 new[] { "Sirenix.OdinValidator" }),

                ("odin_serializer",
                 new[] {
                     "Assets/Plugins/Sirenix/Odin Serializer",
                     "Assets/Plugins/Sirenix/Odin/Modules/Sirenix.OdinSerializer",
                     "Assets/ThirdParty/Sirenix/Odin/Modules/Sirenix.OdinSerializer",
                     "Packages/com.sirenix.odin-serializer"
                 },
                 new[] {
                     "Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.dll",
                     "Assets/Plugins/Sirenix/Assemblies/NoEditor/Sirenix.Serialization.dll",
                     "Assets/Plugins/Sirenix/Assemblies/NoEmitAndNoEditor/Sirenix.Serialization.dll"
                 },
                 new[] { "Sirenix.Serialization" }),

                ("dotween",
                 new[] {
                     "Assets/Demigiant/DOTween",
                     "Assets/Plugins/Demigiant/DOTween",
                     "Assets/Plugins/DOTween",
                     "Assets/ThirdParty/DOTween",
                     "Packages/com.demigiant.dotween"
                 },
                 new[] {
                     "Assets/Plugins/Demigiant/DOTween/DOTween.dll",
                     "Assets/Demigiant/DOTween/DOTween.dll"
                 },
                 new[] { "DOTween" }),

                ("dotween_pro",
                 new[] {
                     "Assets/Demigiant/DOTweenPro",
                     "Assets/Plugins/Demigiant/DOTweenPro",
                     "Assets/Plugins/DOTweenPro",
                     "Assets/ThirdParty/DOTweenPro",
                     "Packages/com.demigiant.dotween-pro"
                 },
                 new[] {
                     "Assets/Plugins/Demigiant/DOTweenPro/DOTweenPro.dll",
                     "Assets/Demigiant/DOTweenPro/DOTweenPro.dll"
                 },
                 new[] { "DOTweenPro" }),
            };

            foreach (var (id, folders, dlls, assemblies) in rules)
            {
                var asset = _config.assets.FirstOrDefault(a => a.id == id);
                if (asset == null) continue;

                bool found = false;

                if (!found)
                {
                    foreach (var folder in folders)
                    {
                        if (Directory.Exists(Path.Combine(projectPath, folder)))
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    foreach (var dll in dlls)
                    {
                        if (File.Exists(Path.Combine(projectPath, dll)))
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    found = CheckAssemblies(assemblies);
                }

                if (found && !asset.installed)
                    detectedCount++;

                asset.installed = found;
            }

            _isDirty = true;
            SaveConfig();
            HideLoading();
            RefreshUI();
        }

        private static bool CheckAssemblies(string[] prefixes)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.IsNullOrEmpty(asm.FullName)) continue;
                foreach (var prefix in prefixes)
                {
                    if (asm.FullName.StartsWith(prefix))
                        return true;
                }
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════
        //  STYLE HELPERS
        // ═══════════════════════════════════════════════════════════

        private static void StyleSmallButton(Button btn, Color bg)
        {
            btn.style.backgroundColor = bg;
            btn.style.color = Color.white;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.fontSize = 10;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.style.borderTopWidth = 0;
            btn.style.borderRightWidth = 0;
            btn.style.borderBottomWidth = 0;
            btn.style.borderLeftWidth = 0;
            btn.style.paddingTop = 3;
            btn.style.paddingBottom = 3;
            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;
            btn.style.height = 22;
        }

        private static void StyleLinkButton(Button btn)
        {
            btn.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            btn.style.color = ColorTeal;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.fontSize = 10;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.style.borderTopWidth = 1;
            btn.style.borderRightWidth = 1;
            btn.style.borderBottomWidth = 1;
            btn.style.borderLeftWidth = 1;
            btn.style.borderTopColor = ColorTeal;
            btn.style.borderRightColor = ColorTeal;
            btn.style.borderBottomColor = ColorTeal;
            btn.style.borderLeftColor = ColorTeal;
            btn.style.paddingTop = 4;
            btn.style.paddingBottom = 4;
            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;
            btn.style.height = 26;
        }

        // ═══════════════════════════════════════════════════════════
        //  COMPILER PATH CONFIGURATION
        // ═══════════════════════════════════════════════════════════

        private void BuildCompilerPathSection()
        {
            var section = new VisualElement();
            section.style.backgroundColor = ColorBgCard;
            section.style.borderTopWidth = 1;
            section.style.borderBottomWidth = 1;
            section.style.borderTopColor = ColorBorder;
            section.style.borderBottomColor = ColorBorder;
            section.style.paddingTop = 10;
            section.style.paddingBottom = 10;
            section.style.paddingLeft = 12;
            section.style.paddingRight = 12;
            section.style.marginBottom = 6;
            section.style.flexShrink = 0;
            section.style.flexGrow = 0;
            section.style.minHeight = 90;

            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;

            var headerIcon = new Label("❖");
            headerIcon.style.fontSize = 14;
            headerIcon.style.marginRight = 4;
            headerIcon.style.color = ColorAmber;
            header.Add(headerIcon);

            var headerLbl = new Label("Compilers");
            headerLbl.style.fontSize = 13;
            headerLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLbl.style.color = ColorAmber;
            header.Add(headerLbl);

            var headerHint = new Label("(auto-detected when blank)");
            headerHint.style.fontSize = 10;
            headerHint.style.color = ColorMuted;
            headerHint.style.marginLeft = 6;
            header.Add(headerHint);

            section.Add(header);

            // CSC row
            var cscRow = CreatePathRow("Code Writer", out _cscPathLabel,
                AutoDetectCscPath, ClearCscPath,
                _config?.defaultCscPath);
            cscRow.style.marginBottom = 4;
            section.Add(cscRow);

            // Spacer
            var spacer = new VisualElement();
            spacer.style.height = 8;
            section.Add(spacer);

            // dotnet row
            var dotnetRow = CreatePathRow("Code Runner", out _dotnetPathLabel,
                AutoDetectDotnetPath, ClearDotnetPath,
                _config?.defaultDotnetPath);
            section.Add(dotnetRow);

            _root.Add(section);
        }

        private VisualElement CreatePathRow(string label, out Label pathLabel,
            Action onDetect, Action onClear, string currentPath)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var nameLbl = new Label(label + ":");
            nameLbl.style.width = 90;
            nameLbl.style.fontSize = 11;
            nameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLbl.style.color = ColorTextSecondary;
            nameLbl.style.overflow = Overflow.Hidden;
            nameLbl.style.textOverflow = TextOverflow.Ellipsis;
            row.Add(nameLbl);

            pathLabel = new Label(string.IsNullOrEmpty(currentPath)
                ? "(auto-detected)"
                : currentPath);
            pathLabel.style.flexGrow = 1;
            pathLabel.style.fontSize = 11;
            pathLabel.style.color = string.IsNullOrEmpty(currentPath)
                ? ColorMuted
                : ColorTextPrimary;
            pathLabel.style.unityTextOverflowPosition = TextOverflowPosition.End;
            pathLabel.style.overflow = Overflow.Hidden;
            pathLabel.style.textOverflow = TextOverflow.Ellipsis;
            row.Add(pathLabel);

            var detectBtn = new Button(onDetect) { text = "🔍 Auto-Find" };
            StyleSmallButton(detectBtn, ColorTealDark);
            detectBtn.style.marginLeft = 8;
            detectBtn.style.width = 90;
            row.Add(detectBtn);

            var clearBtn = new Button(onClear) { text = "Reset" };
            StyleSmallButton(clearBtn, new Color(0.35f, 0.35f, 0.35f));
            clearBtn.style.marginLeft = 4;
            clearBtn.style.width = 50;
            row.Add(clearBtn);

            return row;
        }

        private void UpdateCompilerPathLabels()
        {
            if (_cscPathLabel != null)
            {
                var csc = _config?.defaultCscPath;
                _cscPathLabel.text = string.IsNullOrEmpty(csc)
                    ? "(auto-detected)"
                    : csc;
                _cscPathLabel.style.color = string.IsNullOrEmpty(csc)
                    ? ColorMuted
                    : ColorTextPrimary;
            }
            if (_dotnetPathLabel != null)
            {
                var dotnet = _config?.defaultDotnetPath;
                _dotnetPathLabel.text = string.IsNullOrEmpty(dotnet)
                    ? "(auto-detected)"
                    : dotnet;
                _dotnetPathLabel.style.color = string.IsNullOrEmpty(dotnet)
                    ? ColorMuted
                    : ColorTextPrimary;
            }
        }

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

        private string FindValidCscPath()
        {
            // 1) Saved config
            if (!string.IsNullOrEmpty(_config?.defaultCscPath) && IsValidCscPath(_config.defaultCscPath))
                return _config.defaultCscPath;

            // 2) In-process cache
            if (!string.IsNullOrEmpty(s_CachedCscPath) && IsValidCscPath(s_CachedCscPath))
                return s_CachedCscPath;

            // 3) Collect all candidates with versions, then pick the best
            var candidates = new List<(string path, Version version)>();

            // Unity built-in (lowest priority among valid compilers)
            var unityCsc = FindUnityBuiltInCsc();
            if (!string.IsNullOrEmpty(unityCsc))
                candidates.Add((unityCsc, new Version(0, 0)));

            // Platform-specific SDKs
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    CollectCscCandidatesWindows(candidates);
                    break;
                case RuntimePlatform.OSXEditor:
                    CollectCscCandidatesMacOS(candidates);
                    break;
                default:
                    CollectCscCandidatesLinux(candidates);
                    break;
            }

            if (candidates.Count == 0) return null;

            var minVersion = GetMinimumRecommendedSdkVersion();

            // Prefer compatible versions (>= min), then pick latest
            var compatible = candidates
                .Where(c => c.version >= minVersion)
                .OrderByDescending(c => c.version)
                .FirstOrDefault();

            var best = compatible.path != null
                ? compatible
                : candidates.OrderByDescending(c => c.version).First();

            s_CachedCscPath = best.path;
            return best.path;
        }

        private static string FindUnityBuiltInCsc()
        {
            var appContents = EditorApplication.applicationContentsPath;
            if (string.IsNullOrEmpty(appContents)) return null;

            // Unity's own .NET SDK Roslyn (newer Unity versions)
            var dotNetSdkRoslyn = Path.Combine(appContents, "DotNetSdkRoslyn", "csc.dll");
            if (IsValidCscPath(dotNetSdkRoslyn)) return dotNetSdkRoslyn;

            // MonoBleedingEdge fallback (older Unity versions)
            var name = Application.platform == RuntimePlatform.WindowsEditor ? "csc.exe" : "csc.exe";
            var monoCsc = Path.Combine(appContents, "MonoBleedingEdge", "lib", "mono", "4.5", name);
            if (File.Exists(monoCsc)) return monoCsc;

            // Deep search as last resort (limited depth)
            try
            {
                foreach (var file in Directory.GetFiles(appContents, name, SearchOption.AllDirectories))
                    return file;
            }
            catch { }

            return null;
        }

        private static void CollectCscCandidatesWindows(List<(string path, Version version)> candidates)
        {
            var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // .NET SDK locations
            var sdkRoots = new List<string>();
            if (!string.IsNullOrEmpty(programFilesX86))
                sdkRoots.Add(Path.Combine(programFilesX86, "dotnet", "sdk"));
            if (!string.IsNullOrEmpty(programFiles))
                sdkRoots.Add(Path.Combine(programFiles, "dotnet", "sdk"));
            if (!string.IsNullOrEmpty(userProfile))
                sdkRoots.Add(Path.Combine(userProfile, ".dotnet", "sdk"));

            foreach (var sdkDir in sdkRoots)
            {
                if (!Directory.Exists(sdkDir)) continue;
                foreach (var dir in Directory.GetDirectories(sdkDir))
                {
                    var version = ExtractSdkVersionFromPath(dir);

                    var path = Path.Combine(dir, "Roslyn", "bincore", "csc.exe");
                    if (File.Exists(path)) candidates.Add((path, version));

                    path = Path.Combine(dir, "Roslyn", "csc.exe");
                    if (File.Exists(path)) candidates.Add((path, version));
                }
            }

            // Visual Studio 2022 (ships Roslyn 4.x ≈ .NET 7+)
            if (!string.IsNullOrEmpty(programFiles))
            {
                var vsBase = Path.Combine(programFiles, "Microsoft Visual Studio", "2022");
                if (Directory.Exists(vsBase))
                {
                    foreach (var edition in Directory.GetDirectories(vsBase))
                    {
                        var path = Path.Combine(edition, "MSBuild", "Current", "Bin", "Roslyn", "csc.exe");
                        if (File.Exists(path)) candidates.Add((path, new Version(7, 0)));
                    }
                }
            }

            // JetBrains Rider
            if (!string.IsNullOrEmpty(programFiles))
            {
                var riderBase = Path.Combine(programFiles, "JetBrains");
                if (Directory.Exists(riderBase))
                {
                    foreach (var riderDir in Directory.GetDirectories(riderBase))
                    {
                        if (!riderDir.Contains("Rider")) continue;
                        var path = Path.Combine(riderDir, "tools", "MSBuild", "Current", "Bin", "Roslyn", "csc.exe");
                        if (File.Exists(path)) candidates.Add((path, new Version(0, 0)));
                    }
                }
            }
        }

        private static void CollectCscCandidatesMacOS(List<(string path, Version version)> candidates)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var sdkRoots = new[] {
                "/usr/local/share/dotnet/sdk",
                "/opt/homebrew/share/dotnet/sdk",
                Path.Combine(home, ".dotnet", "sdk")
            };

            foreach (var sdkDir in sdkRoots)
            {
                if (!Directory.Exists(sdkDir)) continue;
                foreach (var dir in Directory.GetDirectories(sdkDir))
                {
                    var version = ExtractSdkVersionFromPath(dir);

                    var path = Path.Combine(dir, "Roslyn", "bincore", "csc.dll");
                    if (IsValidCscPath(path)) candidates.Add((path, version));

                    path = Path.Combine(dir, "Roslyn", "csc.dll");
                    if (IsValidCscPath(path)) candidates.Add((path, version));
                }
            }
        }

        private static void CollectCscCandidatesLinux(List<(string path, Version version)> candidates)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var sdkRoots = new[] {
                "/usr/share/dotnet/sdk",
                "/usr/local/share/dotnet/sdk",
                Path.Combine(home, ".dotnet", "sdk")
            };

            foreach (var sdkDir in sdkRoots)
            {
                if (!Directory.Exists(sdkDir)) continue;
                foreach (var dir in Directory.GetDirectories(sdkDir))
                {
                    var version = ExtractSdkVersionFromPath(dir);

                    var path = Path.Combine(dir, "Roslyn", "bincore", "csc.dll");
                    if (IsValidCscPath(path)) candidates.Add((path, version));

                    path = Path.Combine(dir, "Roslyn", "csc.dll");
                    if (IsValidCscPath(path)) candidates.Add((path, version));
                }
            }
        }

        private string FindValidDotnetPath()
        {
            // 1) Check saved config
            if (!string.IsNullOrEmpty(_config?.defaultDotnetPath) && File.Exists(_config.defaultDotnetPath))
                return _config.defaultDotnetPath;

            // 2) Check in-process cache
            if (!string.IsNullOrEmpty(s_CachedDotnetPath) && File.Exists(s_CachedDotnetPath))
                return s_CachedDotnetPath;

            // 3) Platform-specific standard paths
            string found;
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    found = FindDotnetOnWindows();
                    break;
                case RuntimePlatform.OSXEditor:
                    found = FindDotnetOnMacOS();
                    break;
                default:
                    found = FindDotnetOnLinux();
                    break;
            }
            if (!string.IsNullOrEmpty(found))
            {
                s_CachedDotnetPath = found;
                return found;
            }

            return null;
        }

        private static string FindDotnetOnWindows()
        {
            var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(programFilesX86))
                candidates.Add(Path.Combine(programFilesX86, "dotnet", "dotnet.exe"));
            if (!string.IsNullOrEmpty(programFiles))
                candidates.Add(Path.Combine(programFiles, "dotnet", "dotnet.exe"));
            if (!string.IsNullOrEmpty(userProfile))
                candidates.Add(Path.Combine(userProfile, ".dotnet", "dotnet.exe"));

            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }

            // Search PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var p in pathEnv.Split(';'))
                {
                    var full = Path.Combine(p.Trim(), "dotnet.exe");
                    if (File.Exists(full)) return full;
                }
            }

            return null;
        }

        private static string FindDotnetOnMacOS()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var candidates = new[] {
                "/usr/local/share/dotnet/dotnet",
                "/opt/homebrew/bin/dotnet",
                "/usr/local/bin/dotnet",
                "/usr/bin/dotnet",
                Path.Combine(home, ".dotnet", "dotnet")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }

            // Search PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var p in pathEnv.Split(':'))
                {
                    var full = Path.Combine(p.Trim(), "dotnet");
                    if (File.Exists(full)) return full;
                }
            }

            return null;
        }

        private static string FindDotnetOnLinux()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var candidates = new[] {
                "/usr/share/dotnet/dotnet",
                "/usr/local/share/dotnet/dotnet",
                "/usr/bin/dotnet",
                "/usr/local/bin/dotnet",
                Path.Combine(home, ".dotnet", "dotnet")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }

            // Search PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var p in pathEnv.Split(':'))
                {
                    var full = Path.Combine(p.Trim(), "dotnet");
                    if (File.Exists(full)) return full;
                }
            }

            return null;
        }

        private static bool IsValidCscPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                return true;

            var dir = Path.GetDirectoryName(path);
            var parentDirName = Path.GetFileName(Path.GetDirectoryName(dir));

            // .NET SDK path: sdk/{version}/Roslyn/csc.dll
            // dotnet exec resolves dependencies from the shared framework.
            if (System.Text.RegularExpressions.Regex.IsMatch(parentDirName ?? "", @"^\d+\.\d+"))
                return true;

            // Unity internal path or other standalone deployments:
            // required companion DLLs must exist in the same directory.
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
