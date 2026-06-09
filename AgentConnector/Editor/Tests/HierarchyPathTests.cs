using UnityEngine;
using UnityEditor;

namespace HeraAgent.Tests
{
    /// <summary>
    /// Manual editor tests for HierarchyPath Build/Find round-trips.
    /// Run from the menu: <c>HeraAgent &gt; Tests &gt; HierarchyPath</c>.
    /// </summary>
    public static class HierarchyPathTests
    {
        [MenuItem("HeraAgent/Tests/HierarchyPath")]
        public static void RunTests()
        {
            bool allPassed = true;

            // ---- Test 1: Build simple root ----
            var go = new GameObject("HeraTest_BuildRoot");
            string path = HierarchyPath.Build(go.transform);
            if (path != "/HeraTest_BuildRoot")
            {
                Debug.LogError($"[FAIL] Build root: expected '/HeraTest_BuildRoot', got '{path}'");
                allPassed = false;
            }
            else
            {
                Debug.Log("[PASS] Build root");
            }

            // ---- Test 2: Find by path ----
            var found = HierarchyPath.Find("/HeraTest_BuildRoot");
            if (found != go)
            {
                Debug.LogError($"[FAIL] Find root: expected same object, got {found}");
                allPassed = false;
            }
            else
            {
                Debug.Log("[PASS] Find root");
            }

            // ---- Test 3: Build nested ----
            var child = new GameObject("HeraTest_Child");
            child.transform.SetParent(go.transform);
            string childPath = HierarchyPath.Build(child.transform);
            if (childPath != "/HeraTest_BuildRoot/HeraTest_Child")
            {
                Debug.LogError($"[FAIL] Build nested: expected '/HeraTest_BuildRoot/HeraTest_Child', got '{childPath}'");
                allPassed = false;
            }
            else
            {
                Debug.Log("[PASS] Build nested");
            }

            // ---- Test 4: Find nested ----
            var foundChild = HierarchyPath.Find("/HeraTest_BuildRoot/HeraTest_Child");
            if (foundChild != child)
            {
                Debug.LogError($"[FAIL] Find nested: expected same object, got {foundChild}");
                allPassed = false;
            }
            else
            {
                Debug.Log("[PASS] Find nested");
            }

            // ---- Test 5: Build null ----
            string nullPath = HierarchyPath.Build(null);
            if (nullPath != null)
            {
                Debug.LogError($"[FAIL] Build null: expected null, got '{nullPath}'");
                allPassed = false;
            }
            else
            {
                Debug.Log("[PASS] Build null");
            }

            // ---- Test 6: Find empty ----
            var emptyFound = HierarchyPath.Find("");
            if (emptyFound != null)
            {
                Debug.LogError($"[FAIL] Find empty: expected null, got {emptyFound}");
                allPassed = false;
            }
            else
            {
                Debug.Log("[PASS] Find empty");
            }

            // ---- Test 7: Find non-existent ----
            var missing = HierarchyPath.Find("/HeraTest_DoesNotExist");
            if (missing != null)
            {
                Debug.LogError($"[FAIL] Find missing: expected null, got {missing}");
                allPassed = false;
            }
            else
            {
                Debug.Log("[PASS] Find missing");
            }

            // Cleanup
            Object.DestroyImmediate(child);
            Object.DestroyImmediate(go);

            if (allPassed)
                Debug.Log("[HierarchyPathTests] ALL PASSED");
            else
                Debug.LogError("[HierarchyPathTests] SOME TESTS FAILED");
        }
    }
}
