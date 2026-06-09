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
    public partial class HeraAgentAssetConfigWindow : EditorWindow
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
            titleLbl.style.color = ColorGold;
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
                pill.style.backgroundColor = ColorGold;
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
            countLbl.style.color = active ? ColorGoldDark : ColorDarkMuted;
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
            StyleSmallButton(enableAllBtn, ColorGold);
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

        private VisualElement BuildAssetCard(AssetEntry asset)
        {
            var isSelected = _selectedAssetId == asset.id;

            var card = new VisualElement();
            card.style.backgroundColor = isSelected ? ColorBgCardSelected : ColorBgCard;
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = asset.enabled ? ColorGold : new Color(0.18f, 0.18f, 0.18f);
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
                card.style.borderLeftColor = asset.enabled ? ColorGold : new Color(0.18f, 0.18f, 0.18f);
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
                StyleSmallButton(clearBtn, ColorGold);
                clearBtn.style.marginTop = 12;
                box.Add(clearBtn);
            }

            _contentContainer.Add(box);
            _emptyStateContainer = box;
        }

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
            _loadingLabel.style.color = ColorGold;
            spinnerBox.Add(_loadingLabel);

            var subLbl = new Label("This may take a moment.");
            subLbl.style.fontSize = 10;
            subLbl.style.color = ColorMuted;
            subLbl.style.marginTop = 8;
            spinnerBox.Add(subLbl);

            _loadingOverlay.Add(spinnerBox);
            _root.Add(_loadingOverlay);
        }

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
            _dirtyIndicator.style.color = ColorGold;
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

            var titleLbl = new Label("Hera Agent Unity is not installed");
            titleLbl.style.fontSize = 16;
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLbl.style.color = ColorTextPrimary;
            titleLbl.style.marginBottom = 8;
            root.Add(titleLbl);

            var descLbl = new Label("Hera Agent Unity has not been commissioned yet.\nInstall the CLI and run it once to populate this catalog.");
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

            var cmdLbl = new Label("$ hera-agent-unity asset-config");
            cmdLbl.style.fontSize = 11;
            cmdLbl.style.color = ColorAmber;
            cmdLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            cmdBox.Add(cmdLbl);

            root.Add(cmdBox);
        }

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
            btn.style.color = ColorGold;
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
            btn.style.borderTopColor = ColorGold;
            btn.style.borderRightColor = ColorGold;
            btn.style.borderBottomColor = ColorGold;
            btn.style.borderLeftColor = ColorGold;
            btn.style.paddingTop = 4;
            btn.style.paddingBottom = 4;
            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;
            btn.style.height = 26;
        }

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
            StyleSmallButton(detectBtn, ColorGoldDark);
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

    }
}
