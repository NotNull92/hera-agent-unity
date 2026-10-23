//go:build windows

package cmd

import (
	"errors"
	"fmt"
	"strings"
	"unsafe"

	"golang.org/x/sys/windows"
	"golang.org/x/sys/windows/registry"
)

func addToPATH(_ string) error { return scrubLegacyUserPATH() }

func filterLegacyPATH(value string, valueType uint32) (string, uint32, bool) {
	var entries []string
	for _, entry := range strings.Split(value, ";") {
		entry = strings.TrimSpace(entry)
		norm := strings.TrimRight(strings.ReplaceAll(entry, "/", `\`), `\`)
		leaf := norm[strings.LastIndex(norm, `\`)+1:]
		if entry == "" || strings.EqualFold(leaf, "hera-agent-unity") || strings.EqualFold(leaf, "unity-agent-cli-pro") {
			continue
		}
		entries = append(entries, entry)
	}
	filtered := strings.Join(entries, ";")
	return filtered, valueType, filtered != value
}

func scrubLegacyPATHKey(key registry.Key) (bool, error) {
	value, kind, err := key.GetStringValue("Path")
	if errors.Is(err, registry.ErrNotExist) {
		return false, nil
	}
	if err != nil {
		return false, err
	}
	filtered, kind, changed := filterLegacyPATH(value, kind)
	if !changed {
		return false, nil
	}
	switch kind {
	case registry.EXPAND_SZ:
		err = key.SetExpandStringValue("Path", filtered)
	case registry.SZ:
		err = key.SetStringValue("Path", filtered)
	default:
		return false, fmt.Errorf("unsupported PATH registry type %d", kind)
	}
	return err == nil, err
}

func scrubLegacyUserPATH() error {
	key, err := registry.OpenKey(registry.CURRENT_USER, `Environment`, registry.QUERY_VALUE|registry.SET_VALUE)
	if err != nil {
		return err
	}
	defer func() { _ = key.Close() }()
	changed, err := scrubLegacyPATHKey(key)
	if changed {
		// Notify other applications without letting an unresponsive window block install.
		environment, _ := windows.UTF16PtrFromString("Environment")
		proc := windows.NewLazySystemDLL("user32.dll").NewProc("SendMessageTimeoutW")
		_, _, _ = proc.Call(0xffff, 0x001a, 0, uintptr(unsafe.Pointer(environment)), 0x0002, 1000, 0)
	}
	return err
}
