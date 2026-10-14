using UnityEditor;
using Object = UnityEngine.Object;

namespace HeraAgent
{
    /// <summary>
    /// Version-compatibility shim for the instanceID → EntityId rename, carrying ids as
    /// <see cref="ulong"/> so a single contract spans every supported Unity version.
    /// Unity 6000.3 obsoleted <c>EditorUtility.InstanceIDToObject(int)</c> and
    /// <c>Object.GetInstanceID()</c> as errors (CS0619), replacing them with
    /// <c>EditorUtility.EntityIdToObject(EntityId)</c> and <c>Object.GetEntityId()</c>;
    /// Unity 6000.0-6000.2 lack the new API, hence the version gate.
    /// </summary>
    /// <remarks>
    /// The severity of the <c>int</c> ↔ <c>EntityId</c> conversions moved twice:
    /// through 6000.5 <c>int → EntityId</c> was a warning (CS0618) a localized <c>#pragma</c>
    /// could suppress, while from 6000.6 <b>both</b> directions are obsolete-as-error (CS0619),
    /// which no <c>#pragma</c> can suppress — and at runtime both operators now throw
    /// <c>System.NotImplementedException</c>, as does <c>EditorUtility.InstanceIDToObject(int)</c>.
    /// The 32-bit id therefore cannot resolve an object on 6000.6 at all: the entity id is 64-bit
    /// (measured: scene-asset entity id <c>0x0000130000023230</c>; its low 32 bits do not resolve),
    /// so ids are carried as the full <c>ulong</c> from 6000.6 onward via
    /// <c>EntityId.ToULong</c> / <c>EntityId.FromULong</c>. On 6000.3-6000.5 the vendor's original
    /// reflected-operator technique is retained; below 6000.3 the legacy <c>int</c> API is used
    /// bit-preserved (<c>(ulong)(uint)</c>), so negative legacy ids round-trip unchanged.
    /// <c>EntityId.GetHashCode()</c> is NOT a substitute for the id: on 6000.3.5f2 it returns a
    /// value unrelated to the id (measured live: entity id 104194 hashed to 65781870,
    /// which <see cref="ToObject"/> then failed to resolve).
    /// </remarks>
    internal static class EntityIdCompat
    {
        /// <summary>Get the (instance/entity) id of a Unity object as a 64-bit value.</summary>
        public static ulong IdOf(Object o)
        {
#if UNITY_6000_6_OR_NEWER
            return UnityEngine.EntityId.ToULong(o.GetEntityId());
#elif UNITY_6000_3_OR_NEWER
            var entityId = o.GetEntityId();
            var value = s_EntityIdToInt != null ? s_EntityIdToInt(entityId) : entityId.GetHashCode();
            return (ulong)(uint)value;
#else
            return (ulong)(uint)o.GetInstanceID();
#endif
        }

        /// <summary>Resolve a Unity object from its (instance/entity) id.</summary>
        public static Object ToObject(ulong id)
        {
#if UNITY_6000_6_OR_NEWER
            return EditorUtility.EntityIdToObject(UnityEngine.EntityId.FromULong(id));
#elif UNITY_6000_3_OR_NEWER
            return EditorUtility.EntityIdToObject(ToEntityId((int)(uint)id));
#else
            return EditorUtility.InstanceIDToObject((int)(uint)id);
#endif
        }

#if UNITY_6000_3_OR_NEWER && !UNITY_6000_6_OR_NEWER
        // Unity's EntityId → int implicit operator, bound once per domain via reflection so
        // no compiler on any 6000.3-6000.5 patch line sees a deprecated conversion in source.
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

        private static UnityEngine.EntityId ToEntityId(int id)
        {
            foreach (var method in typeof(UnityEngine.EntityId).GetMethods(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (method.Name != "op_Implicit" || method.ReturnType != typeof(UnityEngine.EntityId))
                    continue;
                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(int))
                {
#pragma warning disable 618 // int → EntityId conversion is deprecated on 6000.3-6000.5.
                    return method.Invoke(null, new object[] { id }) is UnityEngine.EntityId converted
                        ? converted
                        : default(UnityEngine.EntityId);
#pragma warning restore 618
                }
            }
            throw new System.NotSupportedException(
                "UnityEngine.EntityId carries no int → EntityId implicit operator on this Unity " +
                "version, so EntityIdCompat.ToObject cannot convert a legacy int id.");
        }
#endif
    }
}
