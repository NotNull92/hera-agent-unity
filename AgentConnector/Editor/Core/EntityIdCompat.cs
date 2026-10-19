using UnityEditor;
using Object = UnityEngine.Object;

namespace HeraAgent
{
    /// <summary>
    /// Version-compatibility shim for the instanceID → EntityId rename and for the id width that
    /// came with it. Unity 6000.3 obsoleted <c>InstanceIDToObject</c>/<c>GetInstanceID</c> in favour
    /// of <c>EntityIdToObject</c>/<c>GetEntityId</c>; Unity 6000.6 removed the <c>int</c> ↔
    /// <c>EntityId</c> conversions and turned the int-based lookups into runtime stubs.
    /// </summary>
    /// <remarks>
    /// Three paths, chosen at compile time:
    /// <list type="bullet">
    /// <item>6000.6+: an EntityId is 64-bit and its high bits carry a kind tag, so the id is the full
    /// <c>EntityId.ToULong</c> value and the reverse is <c>EntityId.FromULong</c>. Truncating it to
    /// the legacy 32-bit instance id cannot resolve an object here (measured: id
    /// <c>5497558152874</c> has low bits <c>13990</c>, and only the full value resolves).</item>
    /// <item>6000.3-6000.5: the conversions still exist but are deprecated. Both directions are
    /// bound once per domain through reflection so no compiler sees a deprecated call in source, and
    /// an id still fits in an <c>int</c> on these versions.</item>
    /// <item>Below 6000.3: the original int API.</item>
    /// </list>
    /// <c>EntityId.GetHashCode()</c> is not a substitute for either conversion: on 6000.3.5f2 it
    /// returns a value unrelated to the id (entity id 104194 hashed to 65781870, which
    /// <see cref="ToObject"/> then failed to resolve), so it survives only as a last-resort fallback.
    /// </remarks>
    internal static class EntityIdCompat
    {
        /// <summary>Resolve a Unity object from its (instance/entity) id.</summary>
        public static Object ToObject(long id)
        {
#if UNITY_6000_6_OR_NEWER
            // 6000.6 removed the int ↔ EntityId implicit operators and moved identity onto the
            // FromULong/ToULong pair; an EntityId is 64-bit and its high bits carry a kind tag, so
            // only the full value resolves. A value that does not describe a live entity resolves to
            // null rather than to an unrelated object.
            var entityId = UnityEngine.EntityId.FromULong(unchecked((ulong)id));
            if (!UnityEngine.Resources.EntityIdIsValid(entityId)) return null;
            return EditorUtility.EntityIdToObject(entityId);
#elif UNITY_6000_3_OR_NEWER
            // 6000.3-6000.5: int → EntityId is deprecated but present (a warning until 6000.6), and
            // the operator is bound through reflection so no compiler sees it in source.
            return s_IntToEntityId != null ? EditorUtility.EntityIdToObject(s_IntToEntityId(unchecked((int)id))) : null;
#else
            return EditorUtility.InstanceIDToObject(unchecked((int)id));
#endif
        }

        /// <summary>Get the (instance/entity) id of a Unity object as a 64-bit id.</summary>
        public static long IdOf(Object o)
        {
#if UNITY_6000_6_OR_NEWER
            return unchecked((long)UnityEngine.EntityId.ToULong(o.GetEntityId()));
#elif UNITY_6000_3_OR_NEWER
            var entityId = o.GetEntityId();
            return s_EntityIdToInt != null ? s_EntityIdToInt(entityId) : entityId.GetHashCode();
#else
            return o.GetInstanceID();
#endif
        }

#if UNITY_6000_3_OR_NEWER
        // Unity's EntityId → int and int → EntityId implicit operators, bound once per domain via
        // reflection so no compiler on any 6000.x patch line sees a deprecated conversion in source.
        private static readonly System.Func<UnityEngine.EntityId, int> s_EntityIdToInt = BindEntityIdToInt();
        private static readonly System.Func<int, UnityEngine.EntityId> s_IntToEntityId = BindIntToEntityId();

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

        private static System.Func<int, UnityEngine.EntityId> BindIntToEntityId()
        {
            foreach (var method in typeof(UnityEngine.EntityId).GetMethods(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (method.Name != "op_Implicit" || method.ReturnType != typeof(UnityEngine.EntityId))
                    continue;
                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(int))
                    return (System.Func<int, UnityEngine.EntityId>)System.Delegate.CreateDelegate(
                        typeof(System.Func<int, UnityEngine.EntityId>), method);
            }
            return null;
        }
#endif
    }
}
