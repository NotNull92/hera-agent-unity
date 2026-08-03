using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class ProjectIdentityTests
    {
        [MenuItem("HeraAgent/Tests/Project Identity")]
        public static void RunTests()
        {
            var port = 8093;
            var processId = 42;
            Assert(ProjectIdentity.OwnsState(new JObject
            {
                ["project_id"] = ProjectIdentity.CurrentId,
                ["port"] = 9000,
                ["owner_pid"] = 99,
            }, processId), "current project id must win over legacy fields");

            Assert(!ProjectIdentity.OwnsState(new JObject
            {
                ["project_id"] = "sha256:" + new string('f', 64),
                ["port"] = port,
                ["owner_pid"] = processId,
            }, processId), "foreign project state must be rejected");

            Assert(ProjectIdentity.OwnsState(new JObject
            {
                ["port"] = 9000,
                ["owner_pid"] = processId,
            }, processId), "legacy owner pid must be supported");

            Assert(!ProjectIdentity.OwnsState(new JObject
            {
                ["port"] = port,
            }, processId), "unscoped legacy package state must fail closed");

            var root = Path.Combine(Path.GetTempPath(), "AssetsArchive", "Project");
            var assets = Path.Combine(root, "Assets");
            Assert(ProjectIdentity.ResolveRoot(assets) == Path.GetFullPath(root),
                "only the final Assets path segment may be removed");
            Assert(ProjectIdentity.ResolveRoot(root) == Path.GetFullPath(root),
                "non-Assets paths must remain unchanged");

            Debug.Log("[ProjectIdentityTests] ALL PASSED");
        }

        static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("[ProjectIdentityTests] " + message);
        }
    }
}
