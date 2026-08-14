using System;
using System.Linq;
using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using static HeraAgent.Tests.ToolResponseTestSupport;

namespace HeraAgent.Tests
{
    public static class AnimationTimelineToolTests
    {
        public static void RunTests()
        {
            var folder = "Assets/HeraAnimationTimelineTests_" + Guid.NewGuid().ToString("N");
            var folderName = folder.Substring("Assets/".Length);
            Assert.IsFalse(string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", folderName)));

            try
            {
                var clipPath = folder + "/Motion.anim";
                var controllerPath = folder + "/Controller.controller";
                RequireSuccess(ManageAnimation.HandleCommand(new JObject
                {
                    ["action"] = "create_clip",
                    ["path"] = clipPath,
                }));
                foreach (var property in new[] { "m_LocalPosition.x", "m_LocalPosition.y" })
                {
                    RequireSuccess(ManageAnimation.HandleCommand(new JObject
                    {
                        ["action"] = "set_curve",
                        ["path"] = clipPath,
                        ["type"] = "Transform",
                        ["property"] = property,
                        ["keys"] = new JArray
                        {
                            new JObject { ["time"] = 0f, ["value"] = 0f },
                            new JObject { ["time"] = 1f, ["value"] = 1f },
                        },
                    }));
                }

                var removed = RequireSuccess(ManageAnimation.HandleCommand(new JObject
                {
                    ["action"] = "remove_curve",
                    ["path"] = clipPath,
                    ["type"] = "Transform",
                    ["property"] = "m_LocalPosition.x",
                }));
                Assert.AreEqual(2, removed.Value<int>("keys_removed"));
                var clip = RequireSuccess(ManageAnimation.HandleCommand(new JObject
                {
                    ["action"] = "get_clip",
                    ["path"] = clipPath,
                }));
                var bindings = clip["bindings"].OfType<JObject>().ToArray();
                Assert.IsFalse(bindings.Any(binding => binding.Value<string>("property") == "m_LocalPosition.x"));
                Assert.IsTrue(bindings.Any(binding => binding.Value<string>("property") == "m_LocalPosition.y"));
                RequireError(ManageAnimation.HandleCommand(new JObject
                {
                    ["action"] = "remove_curve",
                    ["path"] = clipPath,
                    ["type"] = "Transform",
                    ["property"] = "m_LocalPosition.x",
                }), "CURVE_NOT_FOUND");

                RequireSuccess(ManageAnimation.HandleCommand(new JObject
                {
                    ["action"] = "create_controller",
                    ["path"] = controllerPath,
                }));
                RequireSuccess(ManageAnimation.HandleCommand(new JObject
                {
                    ["action"] = "add_layer",
                    ["path"] = controllerPath,
                    ["name"] = "Upper Body",
                    ["weight"] = 0.5f,
                    ["blending"] = "additive",
                }));
                RequireError(ManageAnimation.HandleCommand(new JObject
                {
                    ["action"] = "add_layer",
                    ["path"] = controllerPath,
                    ["name"] = "Upper Body",
                }), "LAYER_EXISTS");
                var controller = RequireSuccess(ManageAnimation.HandleCommand(new JObject
                {
                    ["action"] = "get_controller",
                    ["path"] = controllerPath,
                }));
                var layers = controller["layers"] as JArray;
                Assert.IsNotNull(layers);
                Assert.AreEqual("Upper Body", layers[1].Value<string>("name"));
                Assert.AreEqual("additive", layers[1].Value<string>("blending"));
                Assert.AreEqual(0.5f, layers[1].Value<float>("weight"), 0.001f);

                if (Type.GetType("UnityEngine.Timeline.TimelineAsset, Unity.Timeline", false) == null)
                    Assert.Ignore("com.unity.timeline is not installed in this fixture.");

                var timelinePath = folder + "/Sequence.playable";
                RequireSuccess(ManageTimeline.Create(new JObject
                {
                    ["path"] = timelinePath,
                    ["frame_rate"] = 30f,
                }));
                RequireSuccess(ManageTimeline.AddTrack(new JObject
                {
                    ["path"] = timelinePath,
                    ["type"] = "Animation",
                    ["name"] = "Motion",
                }));
                RequireSuccess(ManageTimeline.AddClip(new JObject
                {
                    ["path"] = timelinePath,
                    ["track"] = "Motion",
                    ["asset"] = clipPath,
                    ["name"] = "Move",
                    ["start"] = 1.25,
                    ["duration"] = 2.5,
                }));
                RequireError(ManageTimeline.AddTrack(new JObject
                {
                    ["path"] = timelinePath,
                    ["type"] = "MissingTrack",
                }), "TIMELINE_TRACK_TYPE_NOT_FOUND");

                var timeline = RequireSuccess(ManageTimeline.Get(new JObject
                {
                    ["path"] = timelinePath,
                    ["limit"] = 20,
                }));
                Assert.AreEqual(30f, timeline.Value<float>("frame_rate"), 0.001f);
                var tracks = timeline["tracks"] as JArray;
                Assert.IsNotNull(tracks);
                var motionTrack = tracks.OfType<JObject>().Single(track => track.Value<string>("name") == "Motion");
                Assert.AreEqual("Animation", motionTrack.Value<string>("type"));
                Assert.AreEqual("Move", motionTrack["clips"][0].Value<string>("name"));
                Assert.AreEqual(1.25, motionTrack["clips"][0].Value<double>("start"), 0.001);
                Assert.AreEqual(2.5, motionTrack["clips"][0].Value<double>("duration"), 0.001);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
                AssetDatabase.Refresh();
            }
        }

    }
}
