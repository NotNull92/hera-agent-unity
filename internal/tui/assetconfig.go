package tui

import (
	"fmt"
	"strings"

	"github.com/NotNull92/hera-agent-unity/internal/assetconfig"
	"github.com/charmbracelet/bubbles/key"
	"github.com/charmbracelet/bubbles/viewport"
	tea "github.com/charmbracelet/bubbletea"
	"github.com/charmbracelet/lipgloss"
)

// Asset Config TUI styling — aligned with the Old Money palette in style.go.
var (
	titleStyle = lipgloss.NewStyle().
			Bold(true).
			Foreground(lipgloss.Color(ColorPrimary)). // Antique Gold
			MarginBottom(1)

	categoryStyle = lipgloss.NewStyle().
			Bold(true).
			Foreground(lipgloss.Color(ColorSecondary)). // Burgundy
			MarginTop(1)

	checkedStyle = lipgloss.NewStyle().
			Foreground(lipgloss.Color(ColorSuccess)) // Dark Olive Green

	uncheckedStyle = lipgloss.NewStyle().
			Foreground(lipgloss.Color(ColorMuted)) // Charcoal

	installedStyle = lipgloss.NewStyle().
			Foreground(lipgloss.Color(ColorPrimary)). // Antique Gold
			Bold(true)

	notInstalledStyle = lipgloss.NewStyle().
				Foreground(lipgloss.Color(ColorMuted)) // Charcoal

	cursorStyle = lipgloss.NewStyle().
			Foreground(lipgloss.Color(ColorPrimary)). // Antique Gold
			Bold(true)

	helpStyle = lipgloss.NewStyle().
			Foreground(lipgloss.Color(ColorInfo)). // Warm Gray
			MarginTop(1)

	quitStyle = lipgloss.NewStyle().
			Foreground(lipgloss.Color(ColorError)). // Deep Burgundy
			Bold(true)

	boxStyle = lipgloss.NewStyle().
			Border(lipgloss.RoundedBorder()).
			BorderForeground(lipgloss.Color(ColorPrimary)). // Antique Gold accent
			Padding(1, 2)
)

type model struct {
	cfg      *assetconfig.AssetConfig // loaded config — preserved on save (GameFeelMode, compiler paths)
	assets   []assetconfig.AssetEntry
	cursor   int
	quitting bool
	changed  bool
	saving   bool
	saved    bool
	saveErr  error
	save     func(*assetconfig.AssetConfig) error
	width    int
	height   int
	viewport viewport.Model
}

type assetConfigSavedMsg struct {
	err error
}

// KeyMap defines the key bindings.
type keyMap struct {
	Up     key.Binding
	Down   key.Binding
	Toggle key.Binding
	Quit   key.Binding
}

var keys = keyMap{
	Up: key.NewBinding(
		key.WithKeys("up", "k"),
		key.WithHelp("↑/k", "up"),
	),
	Down: key.NewBinding(
		key.WithKeys("down", "j"),
		key.WithHelp("↓/j", "down"),
	),
	Toggle: key.NewBinding(
		key.WithKeys(" ", "enter"),
		key.WithHelp("Space/Enter", "toggle"),
	),
	Quit: key.NewBinding(
		key.WithKeys("q", "esc", "ctrl+c"),
		key.WithHelp("q/Esc", "quit"),
	),
}

// NewAssetConfigModel creates a new TUI model for asset config.
func NewAssetConfigModel() tea.Model {
	cfg, err := assetconfig.Load()
	if err != nil {
		cfg = &assetconfig.AssetConfig{
			Version: "1.0.0",
			Assets:  assetconfig.DefaultAssets(),
		}
	}

	return newAssetConfigModel(cfg, assetconfig.Save)
}

func newAssetConfigModel(cfg *assetconfig.AssetConfig, save func(*assetconfig.AssetConfig) error) model {
	vp := viewport.New(60, 20)
	vp.SetContent("")

	return model{
		cfg:      cfg,
		assets:   cfg.Assets,
		cursor:   0,
		save:     save,
		viewport: vp,
	}
}

func (m model) Init() tea.Cmd {
	return nil
}

func (m model) Update(msg tea.Msg) (tea.Model, tea.Cmd) {
	switch msg := msg.(type) {
	case assetConfigSavedMsg:
		m.saving = false
		if msg.err != nil {
			m.saveErr = msg.err
			m.viewport.SetContent(m.renderContent())
			return m, nil
		}
		m.changed = false
		m.saved = true
		m.quitting = true
		return m, tea.Quit

	case tea.WindowSizeMsg:
		m.width = msg.Width
		m.height = msg.Height
		m.viewport.Width = msg.Width - 4
		m.viewport.Height = msg.Height - 6
		m.viewport.SetContent(m.renderContent())
		return m, nil

	case tea.KeyMsg:
		if m.saving {
			return m, nil
		}
		switch {
		case key.Matches(msg, keys.Quit):
			return m.saveAndQuit()

		case key.Matches(msg, keys.Up):
			if m.cursor > 0 {
				m.cursor--
			}
			m.viewport.SetContent(m.renderContent())
			return m, nil

		case key.Matches(msg, keys.Down):
			if m.cursor < len(m.assets) {
				m.cursor++
			}
			m.viewport.SetContent(m.renderContent())
			return m, nil

		case key.Matches(msg, keys.Toggle):
			if m.cursor < len(m.assets) {
				m.assets[m.cursor].Enabled = !m.assets[m.cursor].Enabled
				m.changed = true
				m.saveErr = nil
			} else {
				return m.saveAndQuit()
			}
			m.viewport.SetContent(m.renderContent())
			return m, nil
		}
	}

	return m, nil
}

func (m model) saveAndQuit() (tea.Model, tea.Cmd) {
	if !m.changed {
		m.quitting = true
		return m, tea.Quit
	}

	m.cfg.Assets = m.assets
	m.saving = true
	m.saveErr = nil
	cfg := m.cfg
	save := m.save
	return m, func() tea.Msg {
		return assetConfigSavedMsg{err: save(cfg)}
	}
}

func (m model) View() string {
	if m.quitting {
		if m.saved {
			return "✓ Asset Config saved\n"
		}
		return "Asset Config closed\n"
	}

	content := m.renderContent()

	if m.width > 0 {
		m.viewport.SetContent(content)
		return boxStyle.Width(m.width).Render(m.viewport.View())
	}

	return boxStyle.Render(content)
}

func (m model) renderContent() string {
	var b strings.Builder

	// Title
	b.WriteString(titleStyle.Render("⚙ Asset Config") + "\n\n")

	// Track which items belong to which category for cursor mapping
	categorized := make(map[string][]int)
	for i, asset := range m.assets {
		cat := asset.Category
		categorized[cat] = append(categorized[cat], i)
	}

	// Render each category
	globalIdx := 0

	for _, cat := range assetconfig.CategoryOrder {
		items, ok := categorized[cat]
		if !ok || len(items) == 0 {
			continue
		}

		catLabel := assetconfig.CategoryNames[cat]
		if catLabel == "" {
			catLabel = cat
		}
		b.WriteString(categoryStyle.Render(catLabel) + "\n")

		for _, idx := range items {
			asset := m.assets[idx]
			line := m.renderItem(asset, globalIdx == m.cursor)
			b.WriteString(line + "\n")
			globalIdx++
		}
	}

	// "Quit" item
	if m.cursor == len(m.assets) {
		b.WriteString(cursorStyle.Render("▸ ") + quitStyle.Render("[ Quit ]"))
	} else {
		b.WriteString("  " + quitStyle.Render("  Quit  "))
	}

	b.WriteString("\n")
	if m.saving {
		b.WriteString(helpStyle.Render("  Saving Asset Config...") + "\n")
	} else if m.saveErr != nil {
		b.WriteString(quitStyle.Render("  Save failed: "+m.saveErr.Error()) + "\n")
	}
	b.WriteString(helpStyle.Render("  ↑↓ move  │  Space toggle  │  q/Esc quit"))

	return b.String()
}

func (m model) renderItem(asset assetconfig.AssetEntry, isSelected bool) string {
	// Checkbox
	var checkbox string
	if asset.Enabled {
		checkbox = checkedStyle.Render("[✓]")
	} else {
		checkbox = uncheckedStyle.Render("[ ]")
	}

	// Installed badge
	var installedBadge string
	if asset.Installed {
		installedBadge = installedStyle.Render(" ✓installed")
	} else {
		installedBadge = notInstalledStyle.Render(" not installed")
	}

	// Name
	name := asset.Name

	// Cursor
	var cursor string
	if isSelected {
		cursor = cursorStyle.Render("▸ ")
	} else {
		cursor = "  "
	}

	// Description (truncated)
	desc := asset.Description
	maxDescLen := 50
	if len(desc) > maxDescLen {
		desc = desc[:maxDescLen-3] + "..."
	}

	// Line 1: cursor + checkbox + name + installed badge
	line1 := fmt.Sprintf("%s%s %s%s", cursor, checkbox, name, installedBadge)

	// Line 2: description (indented)
	line2 := fmt.Sprintf("    %s", desc)

	return fmt.Sprintf("%s\n%s", line1, line2)
}
