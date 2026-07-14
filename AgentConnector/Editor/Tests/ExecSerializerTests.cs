using System.Collections.Generic;
using HeraAgent.Tools;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class ExecSerializerTests
    {
        [MenuItem("HeraAgent/Tests/ExecSerializer")]
        public static void RunTests()
        {
            var go = new GameObject("Hera_ExecSerializer_Test");
            var allPassed = true;

            try
            {
                allPassed &= ExpectShallow("GameObject depth 1", go, 1);
                allPassed &= ExpectShallow("GameObject depth 2", go, 2);
                allPassed &= ExpectShallow("Transform depth 1", go.transform, 1);
                allPassed &= ExpectShallow("Transform depth 2", go.transform, 2);

                var destroyed = new GameObject("Hera_DestroyedExecSerializer_Test");
                UnityEngine.Object.DestroyImmediate(destroyed);
                allPassed &= Expect("Destroyed GameObject depth 1 serializes as null",
                    ExecuteCsharp.SerializeForTesting(destroyed, 1) == null);
                allPassed &= Expect("Destroyed GameObject depth 2 serializes as null",
                    ExecuteCsharp.SerializeForTesting(destroyed, 2) == null);

                var deep = ExecuteCsharp.SerializeForTesting(go.transform, 3)
                    as Dictionary<string, object>;
                allPassed &= Expect("Transform depth 3 reflects members",
                    deep != null && deep.Count > 3);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            if (allPassed)
                Debug.Log("[ExecSerializerTests] ALL PASSED");
            else
                Debug.LogError("[ExecSerializerTests] SOME TESTS FAILED");
        }

        private static bool ExpectShallow(string label, UnityEngine.Object value, int depth)
        {
            var data = ExecuteCsharp.SerializeForTesting(value, depth)
                as Dictionary<string, object>;
            var passed = data != null
                && data.Count == 3
                && data.TryGetValue("name", out var name)
                && data.TryGetValue("type", out var type)
                && data.TryGetValue("instanceID", out var instanceId)
                && (string)name == value.name
                && (string)type == value.GetType().Name
                && (int)instanceId == EntityIdCompat.IdOf(value);
            return Expect(label, passed);
        }

        private static bool Expect(string label, bool passed)
        {
            if (passed)
            {
                Debug.Log("[PASS] " + label);
                return true;
            }

            Debug.LogError("[FAIL] " + label);
            return false;
        }
    }
}
