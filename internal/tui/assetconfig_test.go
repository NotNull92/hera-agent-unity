package tui

import (
	"errors"
	"strings"
	"testing"

	"github.com/NotNull92/hera-agent-unity/internal/assetconfig"
	tea "github.com/charmbracelet/bubbletea"
)

func TestAssetConfigModel_QuitSavesChangesBeforeClosing(t *testing.T) {
	cfg := &assetconfig.AssetConfig{Assets: assetconfig.DefaultAssets()}
	saveCalls := 0
	m := newAssetConfigModel(cfg, func(got *assetconfig.AssetConfig) error {
		saveCalls++
		if !got.Assets[0].Enabled {
			t.Fatal("expected changed assets to be saved")
		}
		return nil
	})
	m.assets[0].Enabled = true
	m.changed = true

	updated, cmd := m.Update(tea.KeyMsg{Type: tea.KeyRunes, Runes: []rune{'q'}})
	if cmd == nil {
		t.Fatal("expected quit to schedule a save")
	}
	m = updated.(model)
	if m.quitting {
		t.Fatal("model closed before the save completed")
	}

	updated, quitCmd := m.Update(cmd())
	if quitCmd == nil {
		t.Fatal("expected successful save to quit")
	}
	m = updated.(model)
	if saveCalls != 1 {
		t.Fatalf("expected one save, got %d", saveCalls)
	}
	if !strings.Contains(m.View(), "Asset Config saved") {
		t.Fatalf("expected saved confirmation, got %q", m.View())
	}
}

func TestAssetConfigModel_SaveFailureStaysOpenAndCanRetry(t *testing.T) {
	cfg := &assetconfig.AssetConfig{Assets: assetconfig.DefaultAssets()}
	saveCalls := 0
	m := newAssetConfigModel(cfg, func(*assetconfig.AssetConfig) error {
		saveCalls++
		if saveCalls == 1 {
			return errors.New("disk full")
		}
		return nil
	})
	m.assets[0].Enabled = true
	m.changed = true

	updated, cmd := m.Update(tea.KeyMsg{Type: tea.KeyRunes, Runes: []rune{'q'}})
	m = updated.(model)
	updated, quitCmd := m.Update(cmd())
	if quitCmd != nil {
		t.Fatal("save failure must not quit")
	}
	m = updated.(model)
	if m.quitting {
		t.Fatal("save failure must keep the model open")
	}
	if !m.changed {
		t.Fatal("save failure must preserve the changed state")
	}
	if !strings.Contains(m.View(), "Save failed: disk full") {
		t.Fatalf("expected visible save error, got %q", m.View())
	}

	updated, cmd = m.Update(tea.KeyMsg{Type: tea.KeyRunes, Runes: []rune{'q'}})
	m = updated.(model)
	updated, quitCmd = m.Update(cmd())
	if quitCmd == nil {
		t.Fatal("expected retry success to quit")
	}
	m = updated.(model)
	if saveCalls != 2 {
		t.Fatalf("expected two save attempts, got %d", saveCalls)
	}
	if !strings.Contains(m.View(), "Asset Config saved") {
		t.Fatalf("expected saved confirmation after retry, got %q", m.View())
	}
}
