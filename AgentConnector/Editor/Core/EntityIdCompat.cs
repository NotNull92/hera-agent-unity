using UnityEditor;
using Object = UnityEngine.Object;

namespace HeraAgent
{
    /// <summary>
    /// Version-compatibility shim for the instanceID → EntityId rename.
    /// Unity 6000.5 promoted <c>EditorUtility.InstanceIDToObject(int)</c> and
    /// <c>Object.GetInstanceID()</c> to obsolete-as-error (CS0619), replacing them
    /// with <c>EditorUtility.EntityIdToObject(EntityId)</c> and <c>Object.GetEntityId()</c>.
    /// Older editors (down to 6000.0) lack the new API, hence the version gate.
    /// </summary>
    /// <remarks>
    /// On the 6000.5 path the <see langword="int"/> ↔ <c>EntityId</c> conversions are
    /// themselves deprecated: <c>int → EntityId</c> is a warning (CS0618), but
    /// <c>EntityId → int</c> is a hard error (CS0619) that cannot be suppressed.
    /// We keep the existing int-based <c>instance_id</c> contract by reading the id via
    /// <c>EntityId.GetHashCode()</c>, whose IL (<c>(uint32)m_rawData</c>) is bit-for-bit
    /// identical to the forbidden <c>EntityId → int</c> operator, so the round-trip with
    /// <see cref="ToObject"/> is exactly Unity's own (deprecated) operator round-trip — no
    /// new correctness risk. The lone surviving <c>int → EntityId</c> warning is localized
    /// and suppressed here.
    /// </remarks>
    internal static class EntityIdCompat
    {
        /// <summary>Resolve a Unity object from its (instance/entity) id.</summary>
        public static Object ToObject(int id)
        {
#if UNITY_6000_5_OR_NEWER
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
#if UNITY_6000_5_OR_NEWER
            // GetHashCode() == low 32 bits of the EntityId == the forbidden (int)EntityId cast.
            return o.GetEntityId().GetHashCode();
#else
            return o.GetInstanceID();
#endif
        }
    }
}
