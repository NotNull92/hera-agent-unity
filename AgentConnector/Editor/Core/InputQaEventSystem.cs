using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HeraAgent
{
    internal static partial class InputQaEventSystem
    {
        public static object State()
        {
            return new SuccessResponse("Input state", InputQaResolver.State());
        }

        public static object Inspect(InputQaOptions options)
        {
            var (inspection, err) = BuildInspection(options);
            if (err != null) return err;
            return new SuccessResponse("Input inspect", options.Details ? inspection.Detailed() : inspection.Compact());
        }

        internal static (InputQaInspection inspection, ErrorResponse err) BuildInspection(InputQaOptions options)
        {
            var (eventSystem, eventErr) = InputQaResolver.ResolveEventSystem();
            if (eventErr != null) return (null, eventErr);
            if (options.Target == null)
                return (null, new ErrorResponse("INPUT_MISSING_TARGET", "Target required: pass instance_id, path, or target."));
            if (!options.Target.activeInHierarchy)
                return (null, new ErrorResponse("INPUT_TARGET_INACTIVE", "Target is inactive in hierarchy."));

            var (point, pointErr) = InputQaResolver.ResolvePoint(options);
            if (pointErr != null) return (null, pointErr);

            var pointer = new PointerEventData(eventSystem)
            {
                pointerId = -1,
                position = point,
                pressPosition = point,
                button = options.Button,
                clickCount = options.ClickCount,
                eligibleForClick = true
            };

            Canvas.ForceUpdateCanvases();
            var raycasts = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, raycasts);
            pointer.pointerCurrentRaycast = raycasts.Count > 0 ? raycasts[0] : default;

            var inspection = new InputQaInspection
            {
                Options = options,
                EventSystem = eventSystem,
                Point = point,
                Pointer = pointer,
                Raycasts = raycasts,
                TopHit = raycasts.Count > 0 ? raycasts[0].gameObject : null
            };
            FillHits(inspection);
            FillInteractability(inspection);
            return (inspection, null);
        }

        private static void FillHits(InputQaInspection inspection)
        {
            for (int i = 0; i < inspection.Raycasts.Count; i++)
            {
                var hit = inspection.Raycasts[i];
                var hitGo = hit.gameObject;
                if (hitGo == null) continue;
                var press = ExecuteEvents.GetEventHandler<IPointerDownHandler>(hitGo);
                var click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitGo);
                bool targetOrChild = IsTargetOrChild(inspection.Options.Target, hitGo);
                bool handlerIsTarget = IsTargetOrChild(inspection.Options.Target, press) ||
                    IsTargetOrChild(inspection.Options.Target, click);

                if (!inspection.TargetHit && (targetOrChild || handlerIsTarget))
                    inspection.TargetHit = true;
                if (i == 0)
                {
                    inspection.PressHandler = press;
                    inspection.ClickHandler = click;
                    inspection.TargetTopHit = targetOrChild || handlerIsTarget;
                    if (!inspection.TargetTopHit) inspection.BlockedBy = hitGo;
                }

                inspection.Hits.Add(new InputQaHit
                {
                    rank = i,
                    instance_id = EntityIdCompat.IdOf(hitGo),
                    name = hitGo.name,
                    path = HierarchyPath.Build(hitGo.transform),
                    module = hit.module == null ? null : hit.module.GetType().Name,
                    distance = hit.distance,
                    target_or_child = targetOrChild,
                    handler_is_target = handlerIsTarget
                });
            }
        }

        private static void FillInteractability(InputQaInspection inspection)
        {
            var selectable = inspection.Options.Target.GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable())
            {
                inspection.Interactable = false;
                inspection.NotInteractableReason = "Selectable.IsInteractable() returned false.";
                return;
            }

            for (var t = inspection.Options.Target.transform; t != null; t = t.parent)
            {
                foreach (var group in t.GetComponents<CanvasGroup>())
                {
                    if (!group.interactable || !group.blocksRaycasts)
                    {
                        inspection.Interactable = false;
                        inspection.NotInteractableReason = $"CanvasGroup on '{t.name}' blocks interaction.";
                        return;
                    }
                    if (group.ignoreParentGroups) return;
                }
            }

            var graphic = inspection.Options.Target.GetComponent<Graphic>();
            if (graphic != null && !graphic.raycastTarget)
            {
                inspection.Interactable = false;
                inspection.NotInteractableReason = "Target Graphic.raycastTarget is false.";
            }
        }

        internal static ErrorResponse ValidateReachable(InputQaInspection inspection)
        {
            if (!inspection.Interactable)
                return new ErrorResponse(
                    "INPUT_TARGET_NOT_INTERACTABLE",
                    inspection.NotInteractableReason ?? "Target is not interactable.",
                    inspection.Detailed());

            if (inspection.Raycasts.Count == 0)
                return new ErrorResponse("INPUT_RAYCAST_MISS", "EventSystem raycast returned no hits at the target point.", inspection.Detailed());

            if (inspection.Options.Strict && !inspection.TargetHit)
                return new ErrorResponse("INPUT_TARGET_NOT_HIT", "Target was not present in the EventSystem raycast stack.", inspection.Detailed());

            if (inspection.Options.Strict && inspection.BlockedBy != null)
                return new ErrorResponse("INPUT_TARGET_BLOCKED", "Target is blocked by another raycast hit.", inspection.Detailed());

            return null;
        }

        internal static object ActionShape(string action, InputQaInspection inspection, List<string> executed)
        {
            return new
            {
                backend = "eventsystem",
                evidence_level = "eventsystem",
                action,
                target_id = EntityIdCompat.IdOf(inspection.Options.Target),
                target_path = HierarchyPath.Build(inspection.Options.Target.transform),
                point = new[] { inspection.Point.x, inspection.Point.y },
                target_top_hit = inspection.TargetTopHit,
                executed,
                blocked_by = inspection.BlockedBy == null ? null : HierarchyPath.Build(inspection.BlockedBy.transform),
                selected_after = InputQaResolver.TargetShape(inspection.EventSystem.currentSelectedGameObject)
            };
        }

        internal static async Task Wait(int frames, int delayMs)
        {
            if (delayMs > 0) await Task.Delay(delayMs);
            while (frames-- > 0)
                await NextEditorUpdate();
        }

        private static Task NextEditorUpdate()
        {
            var source = new TaskCompletionSource<bool>();
            void Tick()
            {
                EditorApplication.update -= Tick;
                source.TrySetResult(true);
            }
            EditorApplication.update += Tick;
            return source.Task;
        }

        internal static bool IsTargetOrChild(GameObject target, GameObject candidate)
        {
            if (target == null || candidate == null) return false;
            return candidate == target || candidate.transform.IsChildOf(target.transform) || target.transform.IsChildOf(candidate.transform);
        }

        internal static RaycastResult RaycastResultOrDefault(this InputQaInspection inspection)
        {
            return inspection.Raycasts.Count > 0 ? inspection.Raycasts[0] : default;
        }
    }
}
