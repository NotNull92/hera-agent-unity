using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HeraAgent
{
    internal sealed class InputQaOptions
    {
        public string Action;
        public string Backend;
        public string Mode;
        public string Key;
        public GameObject Target;
        public Vector2? Position;
        public Vector2? Delta;
        public Vector2? Normalized;
        public Vector2? Offset;
        public Vector2? ScrollDelta;
        public Vector2? ToPosition;
        public Vector2? ToNormalized;
        public PointerEventData.InputButton Button;
        public int ClickCount;
        public int HoldMs;
        public int SettleFrames;
        public int Steps;
        public int MaxResults;
        public bool Strict;
        public bool Details;
        public CancellationToken CancellationToken;
    }

    internal sealed class InputQaCleanupResult
    {
        public bool attempted = true;
        public bool succeeded;
        public string[] released_keys;
        public string[] released_mouse_buttons;
        public string[] errors;
    }

    internal sealed class InputQaHit
    {
        public int rank;
        public int instance_id;
        public string name;
        public string path;
        public string module;
        public float distance;
        public bool target_or_child;
        public bool handler_is_target;
    }

    internal sealed class InputQaInspection
    {
        public InputQaOptions Options;
        public EventSystem EventSystem;
        public int TargetId;
        public string TargetName;
        public string TargetPath;
        public Vector2 Point;
        public PointerEventData Pointer;
        public List<RaycastResult> Raycasts = new List<RaycastResult>();
        public List<InputQaHit> Hits = new List<InputQaHit>();
        public GameObject TopHit;
        public GameObject PressHandler;
        public GameObject ClickHandler;
        public GameObject BlockedBy;
        public string TopHitPath;
        public string PressHandlerPath;
        public string ClickHandlerPath;
        public string BlockedByPath;
        public bool TargetHit;
        public bool TargetTopHit;
        public bool Interactable = true;
        public string NotInteractableReason;
        public bool HitsTruncated;

        public object Compact()
        {
            return new
            {
                backend = "eventsystem",
                evidence_level = "eventsystem",
                target_id = TargetId,
                target_path = TargetPath,
                target_destroyed = Options.Target == null,
                point = new[] { Point.x, Point.y },
                target_hit = TargetHit,
                target_top_hit = TargetTopHit,
                blocked_by = BlockedByPath,
                interactable = Interactable,
                not_interactable_reason = NotInteractableReason
            };
        }

        public object Detailed()
        {
            return new
            {
                backend = "eventsystem",
                evidence_level = "eventsystem",
                target = TargetShape(),
                point = new { screen = new[] { Point.x, Point.y }, source = PointSource() },
                event_system = InputQaResolver.EventSystemShape(EventSystem),
                raycast = new
                {
                    top_hit_path = TopHitPath,
                    target_hit = TargetHit,
                    target_top_hit = TargetTopHit,
                    blocked_by = BlockedBy == null
                        ? (BlockedByPath == null ? null : new { path = BlockedByPath, destroyed = true })
                        : InputQaResolver.TargetShape(BlockedBy),
                    hits = Hits,
                    hits_total = Raycasts.Count,
                    hits_truncated = HitsTruncated
                },
                interactable = Interactable,
                not_interactable_reason = NotInteractableReason,
                handlers = new
                {
                    press = PressHandlerPath,
                    click = ClickHandlerPath
                }
            };
        }

        private object TargetShape()
        {
            if (Options.Target != null) return InputQaResolver.TargetShape(Options.Target);
            return new
            {
                instance_id = TargetId,
                name = TargetName,
                path = TargetPath,
                active = false,
                destroyed = true
            };
        }

        private string PointSource()
        {
            if (Options.Position.HasValue) return "position";
            if (Options.Normalized.HasValue) return "normalized";
            if (Options.Offset.HasValue) return "offset";
            return "rect_center";
        }
    }
}
