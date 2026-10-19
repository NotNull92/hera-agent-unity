using UnityEditor;
using Object = UnityEngine.Object;

namespace HeraAgent
{
    /// <summary>
    /// The id contract the CLI publishes on Unity 6.6+. An EntityId is 64-bit and carries a kind tag
    /// in its high bits, so the id travels as the full <c>EntityId.ToULong</c> value and the reverse
    /// is <c>EntityId.FromULong</c>. Unity 6000.6 removed the <c>int</c> ↔ <c>EntityId</c>
    /// conversions and turned the int-based lookups into runtime stubs, so a 32-bit id cannot resolve
    /// an object on this version.
    /// </summary>
    internal static class EntityIdCompat
    {
        /// <summary>Resolve a Unity object from its 64-bit (instance/entity) id.</summary>
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
    }
}
