using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class EntityIdCompatTests
    {
        [MenuItem("HeraAgent/Tests/EntityIdCompat")]
        public static void RunTests()
        {
            var go = new GameObject("Hera_EntityIdCompat_Test");
            try
            {
                var id = EntityIdCompat.IdOf(go);
                var resolved = EntityIdCompat.ToObject(id);
                if (resolved == go)
                    UnityEngine.Debug.Log("[EntityIdCompatTests] ALL PASSED");
                else
                    UnityEngine.Debug.LogError("[EntityIdCompatTests] round-trip failed");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
