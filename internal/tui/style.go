package tui

import (
	"fmt"

	"github.com/charmbracelet/lipgloss"
)

// Old Money color palette — heritage, restraint, premium
const (
	ColorPrimary   = "#C9A227" // Antique Gold
	ColorSecondary = "#722F37" // Burgundy
	ColorError     = "#8B3A3A" // Deep Burgundy (errors)
	ColorWarning   = "#B8860B" // Dark Goldenrod
	ColorSuccess   = "#556B2F" // Dark Olive Green
	ColorInfo      = "#8B8178" // Warm Gray
	ColorText      = "#F5F1E8" // Cream
	ColorMuted     = "#6B6B6B" // Charcoal
	ColorBorder    = "#2C2C3A" // Deep Navy
	ColorAccent    = "#4A6741" // Sage
)

// Base styles
var (
	TitleStyle       = lipgloss.NewStyle().Bold(true).Foreground(lipgloss.Color(ColorPrimary))
	BoxStyle         = lipgloss.NewStyle().Border(lipgloss.RoundedBorder()).BorderForeground(lipgloss.Color(ColorBorder)).Padding(1, 2)
	BoxAccent        = lipgloss.NewStyle().Border(lipgloss.RoundedBorder()).BorderForeground(lipgloss.Color(ColorPrimary)).Padding(1, 2)
	BoxError         = lipgloss.NewStyle().Border(lipgloss.RoundedBorder()).BorderForeground(lipgloss.Color(ColorError)).Padding(1, 2)
	CheckStyle       = lipgloss.NewStyle().Foreground(lipgloss.Color(ColorSuccess))
	LabelStyle       = lipgloss.NewStyle().Bold(true).Foreground(lipgloss.Color(ColorText))
	PathStyle        = lipgloss.NewStyle().Foreground(lipgloss.Color(ColorPrimary))
	ErrorStyle       = lipgloss.NewStyle().Foreground(lipgloss.Color(ColorError))
	WarningStyle     = lipgloss.NewStyle().Foreground(lipgloss.Color(ColorWarning))
	InfoStyle        = lipgloss.NewStyle().Foreground(lipgloss.Color(ColorInfo))
	SuccessStyle     = lipgloss.NewStyle().Bold(true).Foreground(lipgloss.Color(ColorSuccess))
	MutedStyle       = lipgloss.NewStyle().Foreground(lipgloss.Color(ColorMuted))
	AccentStyle      = lipgloss.NewStyle().Foreground(lipgloss.Color(ColorAccent))
	BoldStyle        = lipgloss.NewStyle().Bold(true)
	HelpSectionStyle = lipgloss.NewStyle().Bold(true).Foreground(lipgloss.Color(ColorSecondary))
	HelpUsageStyle   = lipgloss.NewStyle().Bold(true).Foreground(lipgloss.Color(ColorPrimary))
)

// Status dot colors
var (
	DotReady   = lipgloss.NewStyle().Foreground(lipgloss.Color(ColorSuccess)).Render("●")
	DotWarning = lipgloss.NewStyle().Foreground(lipgloss.Color(ColorWarning)).Render("●")
	DotError   = lipgloss.NewStyle().Foreground(lipgloss.Color(ColorError)).Render("●")
	DotInfo    = lipgloss.NewStyle().Foreground(lipgloss.Color(ColorInfo)).Render("●")
)

// Status panel builder
func StatusPanel(title string, rows [][2]string) string {
	var content string
	for _, row := range rows {
		label := LabelStyle.Width(12).Render(row[0])
		value := row[1]
		content += label + " " + value + "\n"
	}
	return BoxAccent.Render(TitleStyle.Render(title) + "\n\n" + content)
}

// ErrorPanel renders an error in a styled box
func ErrorPanel(title, message string) string {
	return BoxError.Render(ErrorStyle.Render("✗ "+title) + "\n\n" + message)
}

// InfoPanel renders info in a styled box
func InfoPanel(title, message string) string {
	return BoxStyle.Render(InfoStyle.Render(title) + "\n\n" + message)
}

// BrandBanner returns the "HERA AGENT UNITY" wordmark rendered as three rows of
// box-drawing characters in TitleStyle (Antique Gold). Used at the top of the
// install / uninstall flows in place of a plain-text header.
func BrandBanner() string {
	const art = "  ╦ ╦ ╔═╗ ╔═╗ ╔═╗   ╔═╗ ╔═╗ ╔═╗ ╔╗╔ ╔╦╗   ╦ ╦ ╔╗╔ ╦ ╔╦╗ ╦ ╦\n" +
		"  ╠═╣ ║╣  ╠╦╝ ╠═╣   ╠═╣ ║ ╦ ║╣  ║║║  ║    ║ ║ ║║║ ║  ║  ╚╦╝\n" +
		"  ╩ ╩ ╚═╝ ╩╚═ ╩ ╩   ╩ ╩ ╚═╝ ╚═╝ ╝╚╝  ╩    ╚═╝ ╝╚╝ ╩  ╩   ╩ "
	return TitleStyle.Render(art)
}

// Progress renders a progress indicator like [3/10]
func Progress(current, total int) string {
	return InfoStyle.Render(fmt.Sprintf("[%d/%d]", current, total))
}

// StatusBadge renders a colored status badge
func StatusBadge(status string) string {
	switch status {
	case "OK", "PASS", "enabled", "ready", "Success":
		return SuccessStyle.Render(" " + status + " ")
	case "FAIL", "ERROR", "disabled", "failed":
		return ErrorStyle.Render(" " + status + " ")
	case "WARN", "warning", "running":
		return WarningStyle.Render(" " + status + " ")
	default:
		return InfoStyle.Render(" " + status + " ")
	}
}

// DotStatus returns a colored dot + status text
func DotStatus(state string) string {
	switch state {
	case "ready", "playing", "enabled", "OK":
		return DotReady + " " + state
	case "compiling", "waiting", "running":
		return DotWarning + " " + state
	case "stopped", "error", "disabled", "FAIL":
		return DotError + " " + state
	default:
		return DotInfo + " " + state
	}
}
