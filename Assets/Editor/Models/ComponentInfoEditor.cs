using System;
using DefaultEcs.Unity.EntityVisualize.Editor.Extensions;
using UnityEditor;
using Object = UnityEngine.Object;

namespace DefaultEcs.Unity.EntityVisualize.Editor.Models
{
    public partial class ComponentInfo
    {
        public void OnInspectorGUI(EntityInfo entityInfo)
        {
            var changed = false;
            EditorGUI.BeginChangeCheck();
            var newValue = Component;
            if (EntityVisualizer.ComponentDrawers.TryGetValue(ComponentType, out var drawer))
            {
                newValue = drawer(Component);
            }
            else if (ComponentType.IsEnum)
            {
                newValue = EditorGUILayout.EnumPopup((Enum)Component);
            }
            else if (typeof(Object).IsAssignableFrom(ComponentType))
            {
                newValue = EditorGUILayout.ObjectField((Object)Component, ComponentType, false);
            }
            else
            {
                EditorGUILayout.LabelField(Component.ToString());
            }

            if (EditorGUI.EndChangeCheck())
            {
                changed = true;
            }

            if (changed)
            {
                entityInfo.Entity.Set(ComponentType, newValue);
            }
        }
    }
}