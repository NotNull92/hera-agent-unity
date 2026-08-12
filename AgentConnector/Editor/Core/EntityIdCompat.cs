using UnityEditor;
using Object = UnityEngine.Object;

namespace HeraAgent
{
    /// <summary>
    /// Version-compatibility shim for the instanceID → EntityId rename.
    /// Unity 6000.3 introduced the EntityId replacement for <c>EditorUtility.InstanceIDToObject(int)</c> and
    /// <c>Object.GetInstanceID()</c> to obsolete-as-error (CS0619), replacing them
    /// with <c>EditorUtility.EntityIdToObject(EntityId)</c> and <c>Object.GetEntityId()</c>.
    /// Unity 6000.0-6000.2 lack the new API, hence the version gate.
    /// </summary>
    /// <remarks>
    /// On the 6000.3+ path the <see langword="int"/> ↔ <c>EntityId</c> conversions are
    /// themselves deprecated: <c>int → EntityId</c> is a warning (CS0618), but
    /// <c>EntityId → int</c> cannot be written directly on every patch line.
    /// <c>EntityId.GetHashCode()</c> is NOT a substitute: on 6000.3.5f2 it returns a
    /// value unrelated to the id (measured live: entity id 104194 hashed to 65781870,
    /// which <see cref="ToObject"/> then failed to resolve). We therefore invoke Unity's
    /// own <c>EntityId → int</c> implicit operator through a cached reflected delegate —
    /// exact operator semantics with no compile-time obsolete exposure on any version —
    /// and fall back to <c>GetHashCode()</c> only if the operator ever disappears.
    /// The lone surviving <c>int → EntityId</c> warning is localized and suppressed here.
    /// </remarks>
    internal static class EntityIdCompat
    {
        /// <summary>Resolve a Unity object from its (instance/entity) id.</summary>
        public static Object ToObject(int id)
        {
#if UNITY_6000_3_OR_NEWER
#pragma warning disable 618 // int → EntityId conversion is deprecated; only public bridge.
            return EditorUtility.EntityIdToObject(id);
#pragma warning restore 618
#else
            return EditorUtility.InstanceIDToObject(id);
#endif
        }

        /// <summary>Get the (instance/entity) id of a Unity object as an int.</summary>
        public static int IdOf(Object o)
        {
#if UNITY_6000_3_OR_NEWER
            var entityId = o.GetEntityId();
            return s_EntityIdToInt != null ? s_EntityIdToInt(entityId) : entityId.GetHashCode();
#else
            return o.GetInstanceID();
#endif
        }

#if UNITY_6000_3_OR_NEWER
        // Unity's EntityId → int implicit operator, bound once per domain via reflection so
        // no compiler on any 6000.x patch line sees a deprecated conversion in source.
        private static readonly System.Func<UnityEngine.EntityId, int> s_EntityIdToInt = BindEntityIdToInt();

        private static System.Func<UnityEngine.EntityId, int> BindEntityIdToInt()
        {
            foreach (var method in typeof(UnityEngine.EntityId).GetMethods(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (method.Name != "op_Implicit" || method.ReturnType != typeof(int))
                    continue;
                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(UnityEngine.EntityId))
                    return (System.Func<UnityEngine.EntityId, int>)System.Delegate.CreateDelegate(
                        typeof(System.Func<UnityEngine.EntityId, int>), method);
            }
            return null;
        }
#endif
    }
}
