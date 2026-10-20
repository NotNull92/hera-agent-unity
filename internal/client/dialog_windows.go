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
	enabled := user32.NewProc("IsWindowEnabled")
	getOwner := user32.NewProc("GetWindow")
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
		var title [1024]uint16
		_, _, _ = getTitle.Call(hwnd, uintptr(unsafe.Pointer(&title[0])), uintptr(len(title)))
		isEnabled, _, _ := enabled.Call(hwnd)
		ownerHWND, _, _ := getOwner.Call(hwnd, 4) // GW_OWNER
		result = append(result, editorWindow{
			PID: int(owner), HWND: hwnd, Owner: ownerHWND,
			Class: windows.UTF16ToString(class[:]), Title: windows.UTF16ToString(title[:]),
			Visible: true, Enabled: isEnabled != 0,
		})
		return 1
	})
	_, _, _ = enumWindows.Call(callback, 0)
	return result
}
