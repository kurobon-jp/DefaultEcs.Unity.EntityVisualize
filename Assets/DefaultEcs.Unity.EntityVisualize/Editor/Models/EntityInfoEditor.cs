using System;
using System.Collections.Generic;
using DefaultEcs.Unity.EntityVisualize.Editor.Extensions;
using UnityEditor;
using UnityEngine;

namespace DefaultEcs.Unity.EntityVisualize.Editor.Models
{
    public partial class EntityInfo
    {
        private static readonly Dictionary<Type, bool> ComponentFoldouts = new();

        public void OnInspectorGUI()
        {
            var color = 0;
            if (Components.Count > 0)
            {
                using (new GUILayout.VerticalScope("box"))
                {
                    GUILayout.Label("Components", EditorStyles.boldLabel);
                }

                using (new GUILayout.VerticalScope())
                {
                    for (var i = 0; i < Components.Count; i++)
                    {
                        var componentInfo = Components[i];
                        var componentType = componentInfo.ComponentType;
                        ComponentFoldouts.TryGetValue(componentType, out var foldout);
                        var rect = EditorGUILayout.GetControlRect();
                        var width = rect.width;
                        rect.x += 15;
                        rect.width -= 15;
                        EditorGUI.DrawRect(rect, GetRainbowColor(color++));
                        rect.x += 5;
                        rect.width -= 35;
                        foldout = EditorGUI.Foldout(rect, foldout, componentInfo.ComponentName, true);
                        ComponentFoldouts[componentType] = foldout;
                        if (foldout)
                        {
                            EditorGUI.indentLevel += 2;
                            componentInfo.OnInspectorGUI(this);
                            EditorGUI.indentLevel -= 2;
                        }

                        rect.width = 20;
                        rect.x = width - 20;
                        if (GUI.Button(rect, "x", EditorStyles.miniButton))
                        {
                            Entity.Remove(componentType);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the rainbow color using the specified index
        /// </summary>
        /// <param name="index">The index</param>
        /// <returns>The color</returns>
        private static Color GetRainbowColor(int index)
        {
            var color = Color.HSVToRGB(index / 16f % 1f, 1, 1) * 0.25f;
            color.a = 1;
            return color;
        }
    }
}