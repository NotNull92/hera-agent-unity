using System;
using System.Collections.Generic;

namespace HeraAgent.Tools
{
    public static partial class ExecuteCsharp
    {
        private static Dictionary<string, object> SerializeShallowUnityObject(UnityEngine.Object unityObject, Type type)
        {
            if (unityObject == null)
                return null;

            return new Dictionary<string, object>
            {
                ["name"] = unityObject.name,
                ["type"] = type.Name,
                ["instanceID"] = EntityIdCompat.IdOf(unityObject),
            };
        }

        internal static object SerializeForTesting(object obj, int maxDepth)
        {
            return Serialize(obj, 0, ClampDepth(maxDepth),
                new HashSet<object>(ReferenceEqualityComparer.Instance));
        }
    }
}
