using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    /// <summary>
    /// Locks the instance-id contract the CLI publishes: every object's id must round-trip through
    /// <see cref="EntityIdCompat.ToObject"/>. On 6000.6 an EntityId is 64-bit (its high bits carry a
    /// kind tag) and the int-based Unity APIs are gone, so the id is a <c>long</c> end to end.
    /// </summary>
    public static class EntityIdCompatTests
    {
        [MenuItem("HeraAgent/Tests/EntityIdCompat")]
        public static void RunTests()
        {
            if (RunAll())
                Debug.Log("[EntityIdCompatTests] ALL PASSED");
            else
                Debug.LogError("[EntityIdCompatTests] SOME TESTS FAILED");
        }

        internal static bool RunAll()
        {
            var allPassed = true;
            var parent = new GameObject("Hera_EntityIdCompat_Test");
            var child = new GameObject("Hera_EntityIdCompat_Test_Child");
            child.transform.SetParent(parent.transform, false);
            try
            {
                allPassed &= RoundTrip(parent);
                allPassed &= RoundTrip(child);
                allPassed &= RoundTrip(child.transform);

                // A duplicate allocates a fresh id, so the bridge must not depend on an id that was
                // only ever observed once.
                var clone = Object.Instantiate(parent);
                try { allPassed &= RoundTrip(clone); }
                finally { Object.DestroyImmediate(clone); }

                allPassed &= Expect("unknown id resolves to null", EntityIdCompat.ToObject(1) == null);
            }
            catch (System.Exception ex)
            {
                // NUnit reports only the exception type; this line is what makes a future API change
                // diagnosable without another instrumentation round.
                Debug.LogError("[EntityIdCompatTests] threw " + ex.GetType().FullName + ": " + ex.Message +
                               "\n" + ex.StackTrace);
                allPassed = false;
            }
            finally
            {
                Object.DestroyImmediate(child);
                Object.DestroyImmediate(parent);
            }
            return allPassed;
        }

        static bool RoundTrip(Object target)
        {
            var id = EntityIdCompat.IdOf(target);
            var resolved = EntityIdCompat.ToObject(id);
            return Expect("round-trip '" + target.name + "' (id=" + id + ")",
                resolved == target);
        }

        static bool Expect(string label, bool condition)
        {
            if (condition) return true;
            Debug.LogError("[EntityIdCompatTests] failed: " + label);
            return false;
        }
    }
}
