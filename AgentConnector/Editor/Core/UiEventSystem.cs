using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HeraAgent
{
    internal static class UiEventSystem
    {
        internal sealed class Result
        {
            internal GameObject GameObject { get; private set; }
            internal bool Created { get; private set; }
            internal string ErrorCode { get; private set; }
            internal string ErrorMessage { get; private set; }
            internal bool Success => GameObject != null;

            internal static Result Succeeded(GameObject gameObject, bool created) =>
                new Result { GameObject = gameObject, Created = created };

            internal static Result Failed(string code, string message) =>
                new Result { ErrorCode = code, ErrorMessage = message };
        }

        internal static Result Ensure()
        {
            var eventSystemType = ComponentTypeResolver.Resolve("EventSystem");
            if (eventSystemType == null)
                return Result.Failed(
                    "UI_MISSING_EVENTSYSTEM",
                    "EventSystem type not found (com.unity.ugui missing).");

            ResolveInputModules(out var preferredModule, out var incompatibleModule);
            var existing = FindEventSystem(eventSystemType);
            if (existing != null)
            {
                if (preferredModule != null && existing.GetComponent(preferredModule) == null)
                    Undo.AddComponent(existing.gameObject, preferredModule);
                DisableIncompatibleModule(existing, preferredModule, incompatibleModule);
                return Result.Succeeded(existing.gameObject, false);
            }

            var gameObject = new GameObject("EventSystem");
            if (!TryAdd(gameObject, eventSystemType))
            {
                Object.DestroyImmediate(gameObject);
                return Result.Failed(
                    "UI_EVENTSYSTEM_CREATE_FAILED",
                    "Could not add EventSystem.");
            }
            if (preferredModule != null) TryAdd(gameObject, preferredModule);
            Undo.RegisterCreatedObjectUndo(gameObject, "Hera Create EventSystem");
            return Result.Succeeded(gameObject, true);
        }

        static void ResolveInputModules(out Type preferred, out Type incompatible)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            preferred = ComponentTypeResolver.Resolve("InputSystemUIInputModule")
                ?? ComponentTypeResolver.Resolve("StandaloneInputModule");
            incompatible = ComponentTypeResolver.Resolve("StandaloneInputModule");
#elif ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
            preferred = ComponentTypeResolver.Resolve("StandaloneInputModule")
                ?? ComponentTypeResolver.Resolve("InputSystemUIInputModule");
            incompatible = ComponentTypeResolver.Resolve("InputSystemUIInputModule");
#else
            preferred = ComponentTypeResolver.Resolve("StandaloneInputModule")
                ?? ComponentTypeResolver.Resolve("InputSystemUIInputModule");
            incompatible = null;
#endif
        }

        static void DisableIncompatibleModule(
            Component eventSystem,
            Type preferredModule,
            Type incompatibleModule)
        {
            if (incompatibleModule == null || incompatibleModule == preferredModule) return;
            if (!(eventSystem.GetComponent(incompatibleModule) is Behaviour incompatible)
                || !incompatible.enabled) return;
            Undo.RecordObject(incompatible, "Hera disable incompatible UI input module");
            incompatible.enabled = false;
        }

        static Component FindEventSystem(Type eventSystemType)
        {
#if UNITY_6000_5_OR_NEWER
            return Object.FindAnyObjectByType(eventSystemType) as Component;
#elif UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType(eventSystemType) as Component;
#else
            return Object.FindObjectOfType(eventSystemType) as Component;
#endif
        }

        static bool TryAdd(GameObject gameObject, Type componentType)
        {
            try { return gameObject.AddComponent(componentType) != null; }
            catch { return false; }
        }
    }
}
