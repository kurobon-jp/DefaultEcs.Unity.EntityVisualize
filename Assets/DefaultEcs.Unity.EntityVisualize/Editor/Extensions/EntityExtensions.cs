using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DefaultEcs.Unity.EntityVisualize.Editor.Extensions
{
    public static class EntityExtensions
    {
        private static MethodInfo _setMethod;
        private static readonly Dictionary<Type, MethodInfo> GenericSetMethods = new();
        
        private static MethodInfo _removeMethod;
        private static readonly Dictionary<Type, MethodInfo> GenericRemoveMethods = new();
        
        public static void Set(this Entity entity, Type componentType, object component)
        {
            _setMethod ??= typeof(Entity).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .First(x => x.Name == "Set" && x.GetParameters().Length == 1);
            if (!GenericSetMethods.TryGetValue(componentType, out var genericSetMethod))
            {
                GenericSetMethods[componentType]  = genericSetMethod = _setMethod.MakeGenericMethod(componentType);
            }

            genericSetMethod.Invoke(entity, new[] { component });
        }
                
        public static void Remove(this Entity entity, Type componentType)
        {
            _removeMethod ??= typeof(Entity).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .First(x => x.Name == "Remove" && x.GetParameters().Length == 0);
            if (!GenericRemoveMethods.TryGetValue(componentType, out var removeMethod))
            {
                GenericRemoveMethods[componentType]  = removeMethod = _removeMethod.MakeGenericMethod(componentType);
            }

            removeMethod.Invoke(entity, null);
        }
    }
}