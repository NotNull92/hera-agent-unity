using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace HeraAgent
{
    [InitializeOnLoad]
    internal static class InputQaInputSystem
    {
        private const string InputSystemAssembly = "Unity.InputSystem";
        private static readonly HashSet<string> HeldKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> HeldMouseButtons =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static InputQaInputSystem()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseInjectedControls;
        }

        internal static object State()
        {
            var api = Api.TryCreate(out var reason);
            return new SuccessResponse("Input System state", new
            {
                backend = "inputsystem",
                evidence_level = "inputsystem",
                available = Api.PackageAvailable,
                ready = api != null,
                reason,
                package_version = api?.PackageVersion,
                play_mode = Application.isPlaying,
                paused = EditorApplication.isPaused,
                keyboard = DeviceShape(api?.Keyboard),
                mouse = DeviceShape(api?.Mouse),
                injected = new
                {
                    keys = Sorted(HeldKeys),
                    mouse_buttons = Sorted(HeldMouseButtons),
                },
            });
        }

        internal static async Task<object> Keyboard(InputQaOptions options)
        {
            var api = Api.TryCreate(out var reason);
            if (api == null)
                return Unavailable(reason);
            var playModeError = ValidatePlayMode();
            if (playModeError != null)
                return playModeError;
            if (api.Keyboard == null)
                return new ErrorResponse(
                    "INPUTSYSTEM_DEVICE_UNAVAILABLE",
                    "Input System has no current Keyboard device.");
            if (string.IsNullOrWhiteSpace(options.Key))
                return new ErrorResponse(
                    "INPUTSYSTEM_MISSING_KEY",
                    "Keyboard input requires 'key'.");

            if (!TryResolveKey(api, options.Key, out var key, out var canonicalKey))
                return new ErrorResponse(
                    "INPUTSYSTEM_INVALID_KEY",
                    $"Unknown Input System key '{options.Key}'.");
            var control = api.KeyboardIndexer.GetValue(api.Keyboard, new[] { key });
            if (control == null)
                return new ErrorResponse(
                    "INPUTSYSTEM_CONTROL_UNAVAILABLE",
                    $"Keyboard control '{canonicalKey}' is unavailable.");

            var mode = options.Mode ?? "press";
            try
            {
                switch (mode)
                {
                    case "press":
                        if (HeldKeys.Contains(canonicalKey))
                            return AlreadyHeld("key", canonicalKey);
                        await api.ApplyButton(control, true);
                        HeldKeys.Add(canonicalKey);
                        try
                        {
                            await EditorUpdate.Wait(1, options.HoldMs);
                        }
                        finally
                        {
                            await api.ApplyButton(control, false);
                            HeldKeys.Remove(canonicalKey);
                        }
                        break;
                    case "down":
                        if (HeldKeys.Contains(canonicalKey))
                            return AlreadyHeld("key", canonicalKey);
                        await api.ApplyButton(control, true);
                        HeldKeys.Add(canonicalKey);
                        break;
                    case "up":
                        if (!HeldKeys.Contains(canonicalKey))
                            return NotHeld("key", canonicalKey);
                        await api.ApplyButton(control, false);
                        HeldKeys.Remove(canonicalKey);
                        break;
                    default:
                        return InvalidMode("keyboard", mode, "press, down, or up");
                }

                await EditorUpdate.Wait(options.SettleFrames);
                return new SuccessResponse("Input System keyboard", new
                {
                    backend = "inputsystem",
                    evidence_level = "inputsystem",
                    action = "keyboard",
                    mode,
                    key = canonicalKey,
                    pressed_after = api.IsPressed(control),
                    held_by_hera = HeldKeys.Contains(canonicalKey),
                });
            }
            catch (Exception ex)
            {
                return InvocationFailure(ex);
            }
        }

        internal static async Task<object> Mouse(InputQaOptions options)
        {
            var api = Api.TryCreate(out var reason);
            if (api == null)
                return Unavailable(reason);
            var playModeError = ValidatePlayMode();
            if (playModeError != null)
                return playModeError;
            if (api.Mouse == null)
                return new ErrorResponse(
                    "INPUTSYSTEM_DEVICE_UNAVAILABLE",
                    "Input System has no current Mouse device.");

            var mode = options.Mode ?? "click";
            var buttonName = ButtonName(options.Button);
            var button = api.GetMouseControl(buttonName + "Button");
            try
            {
                switch (mode)
                {
                    case "move":
                        if (!options.Position.HasValue)
                            return MissingVector("position", "mouse move");
                        await api.ApplyVector(api.GetMouseControl("position"), options.Position.Value);
                        break;
                    case "delta":
                        if (!options.Delta.HasValue)
                            return MissingVector("delta", "mouse delta");
                        await api.ApplyVector(api.GetMouseControl("delta"), options.Delta.Value);
                        break;
                    case "scroll":
                        if (!options.ScrollDelta.HasValue)
                            return MissingVector("scroll_delta", "mouse scroll");
                        await api.ApplyVector(api.GetMouseControl("scroll"), options.ScrollDelta.Value);
                        break;
                    case "click":
                        if (options.Position.HasValue)
                            await api.ApplyVector(api.GetMouseControl("position"), options.Position.Value);
                        if (HeldMouseButtons.Contains(buttonName))
                            return AlreadyHeld("mouse button", buttonName);
                        await api.ApplyButton(button, true);
                        HeldMouseButtons.Add(buttonName);
                        try
                        {
                            await EditorUpdate.Wait(1, options.HoldMs);
                        }
                        finally
                        {
                            await api.ApplyButton(button, false);
                            HeldMouseButtons.Remove(buttonName);
                        }
                        break;
                    case "down":
                        if (options.Position.HasValue)
                            await api.ApplyVector(api.GetMouseControl("position"), options.Position.Value);
                        if (HeldMouseButtons.Contains(buttonName))
                            return AlreadyHeld("mouse button", buttonName);
                        await api.ApplyButton(button, true);
                        HeldMouseButtons.Add(buttonName);
                        break;
                    case "up":
                        if (options.Position.HasValue)
                            await api.ApplyVector(api.GetMouseControl("position"), options.Position.Value);
                        if (!HeldMouseButtons.Contains(buttonName))
                            return NotHeld("mouse button", buttonName);
                        await api.ApplyButton(button, false);
                        HeldMouseButtons.Remove(buttonName);
                        break;
                    default:
                        return InvalidMode(
                            "mouse",
                            mode,
                            "move, click, down, up, delta, or scroll");
                }

                await EditorUpdate.Wait(options.SettleFrames);
                return new SuccessResponse("Input System mouse", new
                {
                    backend = "inputsystem",
                    evidence_level = "inputsystem",
                    action = "mouse",
                    mode,
                    button = mode == "click" || mode == "down" || mode == "up"
                        ? buttonName
                        : null,
                    position = api.ReadVector(api.GetMouseControl("position")),
                    delta = api.ReadVector(api.GetMouseControl("delta")),
                    scroll = api.ReadVector(api.GetMouseControl("scroll")),
                    pressed_after = button == null ? (bool?)null : api.IsPressed(button),
                    held_by_hera = HeldMouseButtons.Contains(buttonName),
                });
            }
            catch (Exception ex)
            {
                return InvocationFailure(ex);
            }
        }

        private static ErrorResponse ValidatePlayMode()
        {
            if (!Application.isPlaying)
                return new ErrorResponse(
                    "INPUTSYSTEM_PLAY_MODE_REQUIRED",
                    "Input System synthesis requires Play Mode.");
            if (EditorApplication.isPaused)
                return new ErrorResponse(
                    "INPUTSYSTEM_PLAY_MODE_PAUSED",
                    "Input System synthesis is unavailable while Play Mode is paused.");
            return null;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                ReleaseInjectedControls();
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                HeldKeys.Clear();
                HeldMouseButtons.Clear();
            }
        }

        private static void ReleaseInjectedControls()
        {
            try
            {
                var api = Api.TryCreate(out _);
                if (api?.Keyboard != null)
                {
                    foreach (var keyName in Sorted(HeldKeys))
                    {
                        if (TryResolveKey(api, keyName, out var key, out _))
                        {
                            var control = api.KeyboardIndexer.GetValue(
                                api.Keyboard,
                                new[] { key });
                            if (control != null)
                                api.SetButton(control, false);
                        }
                    }
                }

                if (api?.Mouse != null)
                {
                    foreach (var buttonName in Sorted(HeldMouseButtons))
                    {
                        var control = api.GetMouseControl(buttonName + "Button");
                        if (control != null)
                            api.SetButton(control, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Hera Input System cleanup failed: " + Unwrap(ex).Message);
            }
            finally
            {
                HeldKeys.Clear();
                HeldMouseButtons.Clear();
            }
        }

        private static bool TryResolveKey(
            Api api,
            string raw,
            out object key,
            out string canonical)
        {
            var normalized = NormalizeName(raw);
            foreach (var name in Enum.GetNames(api.KeyType))
            {
                if (!string.Equals(NormalizeName(name), normalized, StringComparison.OrdinalIgnoreCase))
                    continue;
                key = Enum.Parse(api.KeyType, name);
                canonical = name;
                return true;
            }

            key = null;
            canonical = null;
            return false;
        }

        private static string NormalizeName(string value)
        {
            return (value ?? string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty);
        }

        private static string ButtonName(UnityEngine.EventSystems.PointerEventData.InputButton button)
        {
            switch (button)
            {
                case UnityEngine.EventSystems.PointerEventData.InputButton.Right:
                    return "right";
                case UnityEngine.EventSystems.PointerEventData.InputButton.Middle:
                    return "middle";
                default:
                    return "left";
            }
        }

        private static object DeviceShape(object device)
        {
            if (device == null)
                return new { present = false };
            var name = FindProperty(device.GetType(), "name", false)?.GetValue(device)?.ToString();
            return new
            {
                present = true,
                name,
                type = device.GetType().FullName,
            };
        }

        private static string[] Sorted(HashSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            Array.Sort(result, StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private static ErrorResponse Unavailable(string reason)
        {
            return new ErrorResponse(
                "INPUTSYSTEM_UNAVAILABLE",
                reason ?? "The optional com.unity.inputsystem package is unavailable.");
        }

        private static ErrorResponse MissingVector(string parameter, string action)
        {
            return new ErrorResponse(
                "INPUTSYSTEM_MISSING_ARGUMENT",
                $"'{parameter}' is required for {action}.");
        }

        private static ErrorResponse AlreadyHeld(string kind, string value)
        {
            return new ErrorResponse(
                "INPUTSYSTEM_ALREADY_HELD",
                $"Hera already holds {kind} '{value}'. Release it before pressing it again.");
        }

        private static ErrorResponse NotHeld(string kind, string value)
        {
            return new ErrorResponse(
                "INPUTSYSTEM_NOT_HELD",
                $"Hera cannot release {kind} '{value}' because it did not press it.");
        }

        private static ErrorResponse InvalidMode(string action, string mode, string expected)
        {
            return new ErrorResponse(
                "INPUTSYSTEM_INVALID_MODE",
                $"Unknown {action} mode '{mode}'. Use {expected}.");
        }

        private static ErrorResponse InvocationFailure(Exception exception)
        {
            var inner = Unwrap(exception);
            return new ErrorResponse(
                "INPUTSYSTEM_INVOCATION_FAILED",
                "Input System rejected the synthesized event: " + inner.Message);
        }

        private static Exception Unwrap(Exception exception)
        {
            while (exception is TargetInvocationException target && target.InnerException != null)
                exception = target.InnerException;
            return exception;
        }

        private static PropertyInfo FindProperty(Type type, string name, bool isStatic)
        {
            if (type == null)
                return null;
            var flags = BindingFlags.Public
                | (isStatic ? BindingFlags.Static : BindingFlags.Instance)
                | BindingFlags.FlattenHierarchy;
            foreach (var property in type.GetProperties(flags))
            {
                var accessor = property.GetGetMethod();
                if (property.Name == name
                    && property.GetIndexParameters().Length == 0
                    && accessor != null
                    && accessor.IsStatic == isStatic)
                {
                    return property;
                }
            }
            return null;
        }

        private sealed class Api
        {
            private const BindingFlags PublicStatic =
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            private const BindingFlags PublicInstance =
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

            internal static bool PackageAvailable =>
                Resolve("UnityEngine.InputSystem.InputSystem") != null;

            internal Type KeyType { get; private set; }
            internal object Keyboard { get; private set; }
            internal object Mouse { get; private set; }
            internal PropertyInfo KeyboardIndexer { get; private set; }
            internal string PackageVersion { get; private set; }

            private MethodInfo StateEventFrom { get; set; }
            private MethodInfo WriteValueIntoEvent { get; set; }
            private MethodInfo ChangeDeviceState { get; set; }
            private MethodInfo UpdateInputSystem { get; set; }
            private EventInfo BeforeUpdate { get; set; }
            private PropertyInfo CurrentUpdateType { get; set; }
            private object TempAllocator { get; set; }
            private object ConfiguredUpdateType { get; set; }
            private object Settings { get; set; }
            private PropertyInfo UpdateMode { get; set; }
            private object ExplicitUpdateMode { get; set; }
            private bool RequiresExplicitUpdate { get; set; }

            internal static Api TryCreate(out string reason)
            {
                var inputSystemType = Resolve("UnityEngine.InputSystem.InputSystem");
                if (inputSystemType == null)
                {
                    reason = "The optional com.unity.inputsystem package is not installed or loaded.";
                    return null;
                }

                var keyboardType = Resolve("UnityEngine.InputSystem.Keyboard");
                var mouseType = Resolve("UnityEngine.InputSystem.Mouse");
                var keyType = Resolve("UnityEngine.InputSystem.Key");
                var inputStateType = Resolve("UnityEngine.InputSystem.LowLevel.InputState");
                var stateEventType = Resolve("UnityEngine.InputSystem.LowLevel.StateEvent");
                var controlExtensionsType = Resolve("UnityEngine.InputSystem.InputControlExtensions");
                var updateType = Resolve("UnityEngine.InputSystem.LowLevel.InputUpdateType");
                var eventPtrType = Resolve("UnityEngine.InputSystem.LowLevel.InputEventPtr");
                var allocatorType = Type.GetType(
                    "Unity.Collections.Allocator, UnityEngine.CoreModule",
                    false);
                var from = FindStateEventFrom(stateEventType);
                var write = FindWriteValueIntoEvent(controlExtensionsType);
                var change = FindChangeDeviceState(inputStateType);
                var update = FindNoArgumentMethod(inputSystemType, "Update", PublicStatic);
                var beforeUpdate = FindEvent(inputSystemType, "onBeforeUpdate");
                var currentUpdateType = FindProperty(
                    inputStateType,
                    "currentUpdateType",
                    true);
                var indexer = FindKeyboardIndexer(keyboardType, keyType);
                var settings = FindProperty(inputSystemType, "settings", true)?.GetValue(null);
                var updateMode = FindProperty(settings?.GetType(), "updateMode", false);
                var configuredUpdateType = ResolveConfiguredUpdateType(
                    updateType,
                    settings,
                    updateMode,
                    out var requiresExplicit,
                    out var explicitUpdateMode);
                if (keyboardType == null || mouseType == null || keyType == null
                    || updateType == null || eventPtrType == null || allocatorType == null
                    || from == null || write == null || change == null
                    || update == null || beforeUpdate == null || currentUpdateType == null
                    || indexer == null || configuredUpdateType == null)
                {
                    reason = "The loaded Input System API is missing a required keyboard, mouse, or event method.";
                    return null;
                }

                reason = null;
                return new Api
                {
                    KeyType = keyType,
                    Keyboard = FindProperty(keyboardType, "current", true)?.GetValue(null),
                    Mouse = FindProperty(mouseType, "current", true)?.GetValue(null),
                    KeyboardIndexer = indexer,
                    PackageVersion = inputSystemType.Assembly.GetName().Version?.ToString(),
                    StateEventFrom = from,
                    WriteValueIntoEvent = write,
                    ChangeDeviceState = change,
                    UpdateInputSystem = update,
                    BeforeUpdate = beforeUpdate,
                    CurrentUpdateType = currentUpdateType,
                    TempAllocator = Enum.Parse(allocatorType, "Temp"),
                    ConfiguredUpdateType = configuredUpdateType,
                    Settings = settings,
                    UpdateMode = updateMode,
                    ExplicitUpdateMode = explicitUpdateMode,
                    RequiresExplicitUpdate = requiresExplicit,
                };
            }

            internal object GetMouseControl(string propertyName)
            {
                return FindProperty(Mouse?.GetType(), propertyName, false)?.GetValue(Mouse);
            }

            internal void SetButton(object control, bool pressed)
            {
                SetValue(control, pressed ? 1f : 0f, typeof(float));
            }

            internal Task ApplyButton(object control, bool pressed)
            {
                return ApplyOnNextConfiguredUpdate(() => SetButton(control, pressed));
            }

            internal Task ApplyVector(object control, Vector2 value)
            {
                return ApplyOnNextConfiguredUpdate(
                    () => SetValue(control, value, typeof(Vector2)));
            }

            private void SetValue(object control, object value, Type valueType)
            {
                if (control == null)
                    throw new InvalidOperationException("Requested Input System control is unavailable.");
                var device = FindProperty(control.GetType(), "device", false)?.GetValue(control);
                if (device == null)
                    throw new InvalidOperationException("Requested Input System control has no device.");
                var fromArguments = new[]
                {
                    device,
                    Activator.CreateInstance(
                        StateEventFrom.GetParameters()[1].ParameterType.GetElementType()),
                    TempAllocator,
                };
                var stateEvent = StateEventFrom.Invoke(null, fromArguments);
                try
                {
                    var eventPtr = fromArguments[1];
                    WriteValueIntoEvent
                        .MakeGenericMethod(valueType)
                        .Invoke(null, new[]
                        {
                            control,
                            value,
                            eventPtr,
                        });
                    ChangeDeviceState.Invoke(null, new[]
                    {
                        device,
                        eventPtr,
                        ConfiguredUpdateType,
                    });
                }
                finally
                {
                    (stateEvent as IDisposable)?.Dispose();
                }
            }

            private Task ApplyOnNextConfiguredUpdate(Action apply)
            {
                var source = new TaskCompletionSource<bool>();
                Action callback = null;
                callback = () =>
                {
                    var current = CurrentUpdateType.GetValue(null);
                    if (!UpdateTypeMatches(current, ConfiguredUpdateType))
                        return;

                    BeforeUpdate.RemoveEventHandler(null, callback);
                    try
                    {
                        apply();
                        source.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        source.TrySetException(Unwrap(ex));
                    }
                };

                BeforeUpdate.AddEventHandler(null, callback);
                if (RequiresExplicitUpdate)
                {
                    try
                    {
                        RunExplicitUpdate();
                        if (!source.Task.IsCompleted)
                        {
                            BeforeUpdate.RemoveEventHandler(null, callback);
                            source.TrySetException(new InvalidOperationException(
                                "Input System did not enter its configured update phase."));
                        }
                    }
                    catch (Exception ex)
                    {
                        BeforeUpdate.RemoveEventHandler(null, callback);
                        source.TrySetException(Unwrap(ex));
                    }
                }
                return source.Task;
            }

            private void RunExplicitUpdate()
            {
                var original = UpdateMode?.GetValue(Settings);
                if (UpdateMode != null && ExplicitUpdateMode != null
                    && !Equals(original, ExplicitUpdateMode))
                {
                    UpdateMode.SetValue(Settings, ExplicitUpdateMode);
                }
                try
                {
                    UpdateInputSystem.Invoke(null, null);
                }
                finally
                {
                    if (UpdateMode != null && original != null
                        && !Equals(original, ExplicitUpdateMode))
                    {
                        UpdateMode.SetValue(Settings, original);
                    }
                }
            }

            internal bool IsPressed(object control)
            {
                if (control == null)
                    return false;
                var property = FindProperty(control.GetType(), "isPressed", false);
                return property != null && property.GetValue(control) is bool pressed && pressed;
            }

            internal float[] ReadVector(object control)
            {
                if (control == null)
                    return null;
                var read = FindNoArgumentMethod(
                    control.GetType(),
                    "ReadValue",
                    PublicInstance,
                    typeof(Vector2));
                if (read == null || !(read.Invoke(control, null) is Vector2 value))
                    return null;
                return new[] { value.x, value.y };
            }

            private static Type Resolve(string fullName)
            {
                return Type.GetType(fullName + ", " + InputSystemAssembly, false);
            }

            private static EventInfo FindEvent(Type type, string name)
            {
                if (type == null)
                    return null;
                foreach (var eventInfo in type.GetEvents(PublicStatic))
                {
                    if (eventInfo.Name == name)
                        return eventInfo;
                }
                return null;
            }

            private static MethodInfo FindStateEventFrom(Type stateEventType)
            {
                if (stateEventType == null)
                    return null;
                foreach (var method in stateEventType.GetMethods(PublicStatic))
                {
                    var parameters = method.GetParameters();
                    if (method.Name == "From"
                        && !method.IsGenericMethod
                        && parameters.Length == 3
                        && parameters[1].ParameterType.IsByRef)
                    {
                        return method;
                    }
                }
                return null;
            }

            private static MethodInfo FindWriteValueIntoEvent(Type extensionsType)
            {
                if (extensionsType == null)
                    return null;
                foreach (var method in extensionsType.GetMethods(PublicStatic))
                {
                    var parameters = method.GetParameters();
                    if (method.Name == "WriteValueIntoEvent"
                        && method.IsGenericMethodDefinition
                        && method.GetGenericArguments().Length == 1
                        && parameters.Length == 3
                        && parameters[0].ParameterType.IsGenericType)
                    {
                        return method;
                    }
                }
                return null;
            }

            private static MethodInfo FindChangeDeviceState(Type inputStateType)
            {
                if (inputStateType == null)
                    return null;
                foreach (var method in inputStateType.GetMethods(PublicStatic))
                {
                    var parameters = method.GetParameters();
                    if (method.Name == "Change"
                        && !method.IsGenericMethod
                        && parameters.Length == 3
                        && parameters[0].ParameterType.FullName
                            == "UnityEngine.InputSystem.InputDevice")
                    {
                        return method;
                    }
                }
                return null;
            }

            private static object ResolveConfiguredUpdateType(
                Type updateType,
                object settings,
                PropertyInfo updateMode,
                out bool requiresExplicit,
                out object explicitUpdateMode)
            {
                var selected = "Dynamic";
                var mode = updateMode?.GetValue(settings)?.ToString();
                requiresExplicit = false;
                if (mode == "ProcessEventsManually")
                {
                    selected = "Manual";
                    requiresExplicit = true;
                }
                else if (mode == "ProcessEventsInFixedUpdate")
                {
                    if (Time.timeScale > 0f)
                        selected = "Fixed";
                    else
                        requiresExplicit = true;
                }

                explicitUpdateMode = updateMode == null
                    ? null
                    : Enum.Parse(
                        updateMode.PropertyType,
                        selected == "Manual"
                            ? "ProcessEventsManually"
                            : selected == "Fixed"
                                ? "ProcessEventsInFixedUpdate"
                                : "ProcessEventsInDynamicUpdate");
                return Enum.Parse(updateType, selected);
            }

            private static bool UpdateTypeMatches(object current, object expected)
            {
                if (current == null || expected == null)
                    return false;
                var currentValue = Convert.ToInt32(current);
                var expectedValue = Convert.ToInt32(expected);
                return (currentValue & expectedValue) == expectedValue;
            }

            private static MethodInfo FindNoArgumentMethod(
                Type type,
                string name,
                BindingFlags flags,
                Type returnType = null)
            {
                foreach (var method in type.GetMethods(flags))
                {
                    if (method.Name == name
                        && !method.IsGenericMethod
                        && method.GetParameters().Length == 0
                        && (returnType == null || method.ReturnType == returnType))
                    {
                        return method;
                    }
                }
                return null;
            }

            private static PropertyInfo FindKeyboardIndexer(Type keyboardType, Type keyType)
            {
                if (keyboardType == null || keyType == null)
                    return null;
                foreach (var property in keyboardType.GetProperties(PublicInstance))
                {
                    var indexes = property.GetIndexParameters();
                    if (indexes.Length == 1 && indexes[0].ParameterType == keyType)
                        return property;
                }
                return null;
            }
        }
    }
}
