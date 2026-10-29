using UnityEditor;
using Object = UnityEngine.Object;

namespace HeraAgent
{
    /// <summary>
    /// Uses 64-bit EntityIds on Unity 6.6+ and signed instance IDs on Unity 2022.3.
    /// IDs belong to the current Editor process; persist global_id across restarts.
    /// </summary>
    internal static class EntityIdCompat
    {
#if UNITY_6000_6_OR_NEWER
        /// <summary>Resolve a 64-bit id in this Editor process; after restart it may identify a different object.</summary>
        public static Object ToObject(long id)
        {
            var entityId = UnityEngine.EntityId.FromULong(unchecked((ulong)id));
            if (!UnityEngine.Resources.EntityIdIsValid(entityId)) return null;
            return EditorUtility.EntityIdToObject(entityId);
        }

        /// <summary>Get the 64-bit (instance/entity) id of a Unity object.</summary>
        public static long IdOf(Object o)
        {
            return unchecked((long)UnityEngine.EntityId.ToULong(o.GetEntityId()));
        }
#elif UNITY_2022_3
        public static Object ToObject(long id)
        {
            if (id < int.MinValue || id > int.MaxValue) return null;
            var instanceId = (int)id;
            return UnityEngine.Resources.InstanceIDIsValid(instanceId)
                ? EditorUtility.InstanceIDToObject(instanceId) : null;
        }

        public static long IdOf(Object o) => o.GetInstanceID();
#else
#error [Hera] I run on Unity 6000.6+, and on Unity 2022.3 in Developer Mode. This Editor is outside both lines.
#endif
    }
}
