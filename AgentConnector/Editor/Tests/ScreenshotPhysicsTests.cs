using System.Collections.Generic;
using System.Linq;
using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HeraAgent.Tests
{
    public static class ScreenshotPhysicsTests
    {
        [MenuItem("HeraAgent/Tests/ScreenshotPhysics")]
        public static void RunTests()
        {
            var allPassed = true;
            allPassed &= TestPhysicsOnlyReturnsClusteredIdentityAndCoordinates();
            allPassed &= TestClusterResultsAreTruncatedAfterGrouping();
            allPassed &= TestCameraCullingMaskConstrainsRequestedLayers();
            allPassed &= TestPhysicsOnlyRejectsPngArgumentsBeforeCapture();
            allPassed &= TestPhysicsBoundsAreStrict();

            if (allPassed)
                Debug.Log("[ScreenshotPhysicsTests] ALL PASSED");
            else
                Debug.LogError("[ScreenshotPhysicsTests] SOME TESTS FAILED");
        }

        private static bool TestPhysicsOnlyReturnsClusteredIdentityAndCoordinates()
        {
            var fixture = CreateFixture();
            try
            {
                var response = EditorScreenshot.HandleCommand(new JObject
                {
                    ["physics_only"] = true,
                    ["physics_grid_size"] = 3,
                    ["max_physics_hits"] = 8,
                    ["physics_layer_mask"] = 1 << fixture.target.layer,
                    ["physics_max_distance"] = 50f,
                    ["physics_query_triggers"] = "ignore",
                }) as SuccessResponse;
                var data = response == null ? null : JObject.FromObject(response.data);
                var annotations = data?["physics_annotations"] as JArray;
                var annotation = annotations?.OfType<JObject>().SingleOrDefault();
                var raycast = data?["physics_raycast"] as JObject;
                var camera = raycast?["camera"] as JObject;
                var coordinateSpaces = data?["coordinate_spaces"] as JObject;

                var passed = true;
                passed &= Expect("PhysicsOnlySkipsPixels",
                    response?.success == true
                    && data?.Value<bool>("pixels_requested") == false
                    && data["path"] == null);
                passed &= Expect("PhysicsGridIsBoundedAndClustered",
                    raycast?.Value<int>("grid_size") == 3
                    && raycast.Value<int>("rays_cast") == 9
                    && raycast.Value<int>("rays_hit") == 9
                    && data.Value<int>("physics_annotation_count") == 1
                    && annotation?.Value<int>("sample_count") == 9);
                passed &= Expect("PhysicsIdentity",
                    annotation?.Value<int>("instance_id") == EntityIdCompat.IdOf(fixture.target)
                    && annotation.Value<int>("collider_instance_id")
                        == EntityIdCompat.IdOf(fixture.targetCollider)
                    && annotation.Value<string>("hierarchy_path") == "/!HeraPhysicsTarget"
                    && annotation.Value<string>("collider_type") == "BoxCollider");
                passed &= Expect("PhysicsCameraAndLayerConstraints",
                    camera?.Value<int>("instance_id") == EntityIdCompat.IdOf(fixture.camera.gameObject)
                    && raycast.Value<int>("requested_layer_mask") == 1 << fixture.target.layer
                    && raycast.Value<int>("effective_layer_mask") == 1 << fixture.target.layer
                    && raycast.Value<string>("query_triggers") == "ignore");
                passed &= Expect("PhysicsCoordinates",
                    annotation?["input_point"] is JArray inputPoint
                    && inputPoint.Count == 2
                    && annotation["image_point"] is JArray imagePoint
                    && imagePoint.Count == 2
                    && annotation["input_bounds"] is JArray inputBounds
                    && inputBounds.Count == 4
                    && annotation["image_bounds"] is JArray imageBounds
                    && imageBounds.Count == 4
                    && annotation["world_point"] is JArray worldPoint
                    && worldPoint.Count == 3
                    && annotation["world_normal"] is JArray worldNormal
                    && worldNormal.Count == 3);
                passed &= Expect("PhysicsCoordinateSpaces",
                    coordinateSpaces?["input"]?.Value<string>("name")
                        == "unity_screen_bottom_left_pixels"
                    && coordinateSpaces["image"]?.Value<string>("name")
                        == "game_view_top_left_pixels");
                return passed;
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static bool TestClusterResultsAreTruncatedAfterGrouping()
        {
            var fixture = CreateFixture();
            GameObject rightTarget = null;
            try
            {
                fixture.target.transform.position = new Vector3(-2.8f, 0f, 5f);
                fixture.target.transform.localScale = new Vector3(3.5f, 20f, 1f);
                rightTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rightTarget.name = "!HeraPhysicsTargetRight";
                rightTarget.layer = fixture.target.layer;
                rightTarget.transform.position = new Vector3(2.8f, 0f, 5f);
                rightTarget.transform.localScale = new Vector3(3.5f, 20f, 1f);

                var response = EditorScreenshot.HandleCommand(new JObject
                {
                    ["physics_only"] = true,
                    ["physics_grid_size"] = 3,
                    ["max_physics_hits"] = 1,
                    ["physics_layer_mask"] = 1 << fixture.target.layer,
                }) as SuccessResponse;
                var data = response == null ? null : JObject.FromObject(response.data);
                return Expect(
                    nameof(TestClusterResultsAreTruncatedAfterGrouping),
                    data?.Value<int>("physics_annotation_count") == 1
                    && data.Value<int>("physics_annotations_total") == 2
                    && data.Value<bool>("physics_annotations_truncated")
                    && data.Value<int>("physics_annotations_limit") == 1);
            }
            finally
            {
                if (rightTarget != null) Object.DestroyImmediate(rightTarget);
                fixture.Dispose();
            }
        }

        private static bool TestCameraCullingMaskConstrainsRequestedLayers()
        {
            var fixture = CreateFixture();
            try
            {
                var response = EditorScreenshot.HandleCommand(new JObject
                {
                    ["physics_only"] = true,
                    ["physics_layer_mask"] = 1 << 28,
                }) as ErrorResponse;
                return Expect(
                    nameof(TestCameraCullingMaskConstrainsRequestedLayers),
                    response?.code == "SCREENSHOT_PHYSICS_LAYER_MASK_EMPTY");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static bool TestPhysicsOnlyRejectsPngArgumentsBeforeCapture()
        {
            var response = EditorScreenshot.HandleCommand(new JObject
            {
                ["physics_only"] = true,
                ["output_path"] = "Screenshots/should-not-exist.png",
            }) as ErrorResponse;
            return Expect(
                nameof(TestPhysicsOnlyRejectsPngArgumentsBeforeCapture),
                response?.code == "SCREENSHOT_PHYSICS_ONLY_OUTPUT_CONFLICT");
        }

        private static bool TestPhysicsBoundsAreStrict()
        {
            var contract = ToolContractRegistry.Get("screenshot");
            var properties = contract?.InputSchema["properties"];
            var invalidGrid = ToolContractValidator.Validate(
                contract,
                new JObject
                {
                    ["physics_only"] = true,
                    ["physics_grid_size"] = 17,
                });
            var invalidHits = ToolContractValidator.Validate(
                contract,
                new JObject
                {
                    ["physics_only"] = true,
                    ["max_physics_hits"] = 101,
                });
            var invalidDistance = ToolContractValidator.Validate(
                contract,
                new JObject
                {
                    ["physics_only"] = true,
                    ["physics_max_distance"] = 0,
                });
            return Expect(
                nameof(TestPhysicsBoundsAreStrict),
                properties?["physics_grid_size"]?.Value<int>("maximum") == 16
                && properties["max_physics_hits"]?.Value<int>("maximum") == 100
                && properties["physics_max_distance"]?.Value<float>("minimum") == 0.0001f
                && invalidGrid.Error?.code == "INVALID_ARGUMENT"
                && invalidHits.Error?.code == "INVALID_ARGUMENT"
                && invalidDistance.Error?.code == "INVALID_ARGUMENT");
        }

        private static PhysicsFixture CreateFixture()
        {
            var existingCameras = Resources.FindObjectsOfTypeAll<Camera>()
                .Where(camera => camera != null && camera.gameObject.scene.IsValid())
                .ToDictionary(camera => camera, camera => camera.enabled);
            foreach (var camera in existingCameras.Keys) camera.enabled = false;

            var cameraObject = new GameObject("!HeraPhysicsCamera");
            cameraObject.tag = "MainCamera";
            var fixtureCamera = cameraObject.AddComponent<Camera>();
            fixtureCamera.nearClipPlane = 0.1f;
            fixtureCamera.farClipPlane = 100f;
            fixtureCamera.cullingMask = (1 << 29) | (1 << 30);
            fixtureCamera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var excluded = GameObject.CreatePrimitive(PrimitiveType.Cube);
            excluded.name = "!HeraPhysicsExcluded";
            excluded.layer = 29;
            excluded.transform.position = new Vector3(0f, 0f, 3f);
            excluded.transform.localScale = new Vector3(20f, 20f, 1f);

            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "!HeraPhysicsTarget";
            target.layer = 30;
            target.transform.position = new Vector3(0f, 0f, 5f);
            target.transform.localScale = new Vector3(20f, 20f, 1f);

            return new PhysicsFixture(
                fixtureCamera,
                target,
                target.GetComponent<BoxCollider>(),
                excluded,
                existingCameras);
        }

        private sealed class PhysicsFixture
        {
            public readonly Camera camera;
            public readonly GameObject target;
            public readonly BoxCollider targetCollider;
            private readonly GameObject _excluded;
            private readonly Dictionary<Camera, bool> _existingCameras;

            public PhysicsFixture(
                Camera camera,
                GameObject target,
                BoxCollider targetCollider,
                GameObject excluded,
                Dictionary<Camera, bool> existingCameras)
            {
                this.camera = camera;
                this.target = target;
                this.targetCollider = targetCollider;
                _excluded = excluded;
                _existingCameras = existingCameras;
            }

            public void Dispose()
            {
                if (target != null) Object.DestroyImmediate(target);
                if (_excluded != null) Object.DestroyImmediate(_excluded);
                if (camera != null) Object.DestroyImmediate(camera.gameObject);
                foreach (var entry in _existingCameras)
                    if (entry.Key != null) entry.Key.enabled = entry.Value;
            }
        }

        private static bool Expect(string name, bool condition)
        {
            if (condition) Debug.Log("[PASS] " + name);
            else Debug.LogError("[FAIL] " + name);
            return condition;
        }
    }
}
