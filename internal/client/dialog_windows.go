package client

import (
	"unsafe"

	"golang.org/x/sys/windows"
)

func enumerateEditorWindows(pid int) []editorWindow {
	user32 := windows.NewLazySystemDLL("user32.dll")
	enumWindows := user32.NewProc("EnumWindows")
	getPID := user32.NewProc("GetWindowThreadProcessId")
	visible := user32.NewProc("IsWindowVisible")
	getClass := user32.NewProc("GetClassNameW")
	getTitle := user32.NewProc("GetWindowTextW")
	var result []editorWindow
	callback := windows.NewCallback(func(hwnd, _ uintptr) uintptr {
		var owner uint32
		_, _, _ = getPID.Call(hwnd, uintptr(unsafe.Pointer(&owner)))
		if int(owner) != pid {
			return 1
		}
		shown, _, _ := visible.Call(hwnd)
		if shown == 0 {
			return 1
		}
		var class [256]uint16
		_, _, _ = getClass.Call(hwnd, uintptr(unsafe.Pointer(&class[0])), uintptr(len(class)))
		if windows.UTF16ToString(class[:]) != "#32770" {
			return 1
		}
		var title [1024]uint16
		_, _, _ = getTitle.Call(hwnd, uintptr(unsafe.Pointer(&title[0])), uintptr(len(title)))
		result = append(result, editorWindow{PID: int(owner), Class: "#32770", Title: windows.UTF16ToString(title[:]), Visible: true})
		return 1
	})
	_, _, _ = enumWindows.Call(callback, 0)
	return result
}
