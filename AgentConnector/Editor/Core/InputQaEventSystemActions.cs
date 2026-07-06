using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HeraAgent
{
    internal static partial class InputQaEventSystem
    {
        public static async Task<object> Click(InputQaOptions options)
        {
            var (inspection, err) = BuildInspection(options);
            if (err != null) return err;
            var reachable = ValidateReachable(inspection);
            if (reachable != null) return reachable;

            var clickTarget = inspection.ClickHandler;
            var pressTarget = inspection.PressHandler ?? clickTarget;
            if (clickTarget == null)
                return new ErrorResponse("INPUT_HANDLER_NOT_FOUND", "No IPointerClickHandler found for the top raycast hit.", inspection.Detailed());
            if (options.Strict && !IsTargetOrChild(options.Target, clickTarget))
                return new ErrorResponse("INPUT_TARGET_BLOCKED", "Top raycast hit resolves to a different click handler.", inspection.Detailed());

            var data = PreparePressData(inspection, pressTarget, clickTarget);
            var executed = new List<string>();
            ExecutePointerEnterDown(inspection, data, pressTarget, executed);
            await Wait(options.HoldMs > 0 ? 1 : 0, options.HoldMs);
            ExecutePointerUpClick(data, pressTarget, clickTarget, executed);
            inspection.EventSystem.SetSelectedGameObject(options.Target, data);
            await Wait(options.SettleFrames, 0);

            if (options.Strict && !executed.Contains("click"))
                return new ErrorResponse("INPUT_HANDLER_NOT_EXECUTED", "Pointer click handler did not execute.", inspection.Detailed());
            return new SuccessResponse("Input click", ActionShape("click", inspection, executed));
        }

        public static async Task<object> PointerDown(InputQaOptions options)
        {
            var (inspection, err) = BuildInspection(options);
            if (err != null) return err;
            var reachable = ValidateReachable(inspection);
            if (reachable != null) return reachable;

            var pressTarget = inspection.PressHandler ?? inspection.ClickHandler;
            if (pressTarget == null)
                return new ErrorResponse("INPUT_HANDLER_NOT_FOUND", "No pointer down/click handler found for the top raycast hit.", inspection.Detailed());

            var executed = new List<string>();
            ExecutePointerEnterDown(inspection, PreparePressData(inspection, pressTarget, inspection.ClickHandler), pressTarget, executed);
            await Wait(options.SettleFrames, 0);
            return new SuccessResponse("Input pointer_down", ActionShape("pointer_down", inspection, executed));
        }

        public static async Task<object> PointerUp(InputQaOptions options)
        {
            var (inspection, err) = BuildInspection(options);
            if (err != null) return err;
            var reachable = ValidateReachable(inspection);
            if (reachable != null) return reachable;

            var pressTarget = inspection.PressHandler ?? inspection.ClickHandler;
            if (pressTarget == null)
                return new ErrorResponse("INPUT_HANDLER_NOT_FOUND", "No pointer up/click handler found for the top raycast hit.", inspection.Detailed());

            var executed = new List<string>();
            var data = PreparePressData(inspection, pressTarget, inspection.ClickHandler);
            if (ExecuteEvents.Execute(pressTarget, data, ExecuteEvents.pointerUpHandler))
                executed.Add("up");
            await Wait(options.SettleFrames, 0);
            return new SuccessResponse("Input pointer_up", ActionShape("pointer_up", inspection, executed));
        }

        public static async Task<object> Submit(InputQaOptions options)
        {
            var (inspection, err) = BuildInspection(options);
            if (err != null) return err;
            var handler = ExecuteEvents.GetEventHandler<ISubmitHandler>(options.Target);
            if (handler == null)
                return new ErrorResponse("INPUT_HANDLER_NOT_FOUND", "No ISubmitHandler found on the target hierarchy.", inspection.Detailed());

            var data = new BaseEventData(inspection.EventSystem);
            inspection.EventSystem.SetSelectedGameObject(options.Target, data);
            bool submitted = ExecuteEvents.Execute(handler, data, ExecuteEvents.submitHandler);
            await Wait(options.SettleFrames, 0);
            if (options.Strict && !submitted)
                return new ErrorResponse("INPUT_HANDLER_NOT_EXECUTED", "Submit handler did not execute.", inspection.Detailed());
            return new SuccessResponse("Input submit", ActionShape("submit", inspection, new List<string> { "submit" }));
        }

        public static async Task<object> Scroll(InputQaOptions options)
        {
            var (inspection, err) = BuildInspection(options);
            if (err != null) return err;
            var reachable = ValidateReachable(inspection);
            if (reachable != null) return reachable;

            var data = inspection.Pointer;
            data.scrollDelta = options.ScrollDelta ?? new Vector2(0f, -1f);
            var scrollHandler = ExecuteEvents.ExecuteHierarchy(inspection.TopHit ?? options.Target, data, ExecuteEvents.scrollHandler);
            bool scrolled = scrollHandler != null;
            await Wait(options.SettleFrames, 0);
            if (options.Strict && !scrolled)
                return new ErrorResponse("INPUT_HANDLER_NOT_EXECUTED", "Scroll handler did not execute.", inspection.Detailed());
            return new SuccessResponse("Input scroll", ActionShape("scroll", inspection, scrolled ? new List<string> { "scroll" } : new List<string>()));
        }

        public static async Task<object> Drag(InputQaOptions options)
        {
            var (inspection, err) = BuildInspection(options);
            if (err != null) return err;
            var reachable = ValidateReachable(inspection);
            if (reachable != null) return reachable;

            var dragTarget = ExecuteEvents.GetEventHandler<IDragHandler>(inspection.TopHit ?? options.Target);
            if (dragTarget == null)
                return new ErrorResponse("INPUT_HANDLER_NOT_FOUND", "No IDragHandler found for the top raycast hit.", inspection.Detailed());

            var (endPoint, pointErr) = InputQaResolver.ResolveDragEndPoint(options);
            if (pointErr != null) return pointErr;

            var data = PreparePressData(inspection, inspection.PressHandler ?? dragTarget, inspection.ClickHandler);
            data.pointerDrag = dragTarget;
            var executed = new List<string>();
            ExecuteEvents.Execute(dragTarget, data, ExecuteEvents.initializePotentialDrag);
            executed.Add("initialize_potential_drag");
            if (ExecuteEvents.Execute(dragTarget, data, ExecuteEvents.beginDragHandler)) executed.Add("begin_drag");

            var steps = Mathf.Max(1, options.Steps);
            var current = inspection.Point;
            for (int i = 1; i <= steps; i++)
            {
                var next = Vector2.Lerp(inspection.Point, endPoint, i / (float)steps);
                data.delta = next - current;
                data.position = next;
                data.dragging = true;
                if (ExecuteEvents.Execute(dragTarget, data, ExecuteEvents.dragHandler)) executed.Add("drag");
                current = next;
                await Wait(1, 0);
            }

            if (ExecuteEvents.Execute(dragTarget, data, ExecuteEvents.endDragHandler)) executed.Add("end_drag");
            await Wait(options.SettleFrames, 0);
            if (options.Strict && !executed.Contains("drag"))
                return new ErrorResponse("INPUT_HANDLER_NOT_EXECUTED", "Drag handler did not execute.", inspection.Detailed());
            return new SuccessResponse("Input drag", ActionShape("drag", inspection, executed));
        }

        private static PointerEventData PreparePressData(InputQaInspection inspection, GameObject pressTarget, GameObject clickTarget)
        {
            var data = inspection.Pointer;
            data.pointerPressRaycast = inspection.RaycastResultOrDefault();
            data.pointerCurrentRaycast = inspection.RaycastResultOrDefault();
            data.pointerPress = pressTarget;
            data.rawPointerPress = inspection.TopHit;
            data.pointerClick = clickTarget;
            data.pointerEnter = inspection.TopHit;
            data.eligibleForClick = true;
            data.clickTime = Time.unscaledTime;
            return data;
        }

        private static void ExecutePointerEnterDown(InputQaInspection inspection, PointerEventData data, GameObject pressTarget, List<string> executed)
        {
            if (inspection.TopHit != null && ExecuteEvents.Execute(inspection.TopHit, data, ExecuteEvents.pointerEnterHandler))
                executed.Add("enter");
            if (pressTarget != null && ExecuteEvents.Execute(pressTarget, data, ExecuteEvents.pointerDownHandler))
                executed.Add("down");
        }

        private static void ExecutePointerUpClick(PointerEventData data, GameObject pressTarget, GameObject clickTarget, List<string> executed)
        {
            if (pressTarget != null && ExecuteEvents.Execute(pressTarget, data, ExecuteEvents.pointerUpHandler))
                executed.Add("up");
            if (clickTarget != null && ExecuteEvents.Execute(clickTarget, data, ExecuteEvents.pointerClickHandler))
                executed.Add("click");
        }
    }
}
