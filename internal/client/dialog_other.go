//go:build !windows

package client

func enumerateEditorWindows(int) []editorWindow { return nil }
