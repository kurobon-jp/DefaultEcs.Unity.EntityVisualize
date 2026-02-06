#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DefaultEcs.Unity.EntityVisualize
{
    /// <summary>
    /// The entity visualizer class
    /// </summary>
    public static class EntityVisualizer
    {
        internal static Dictionary<string, World> Worlds { get; } = new();

        public static event Action<string, World> OnRegistered;

        public static readonly Dictionary<Type, Func<object, object>> ComponentDrawers = new()
        {
            {
                typeof(string), value => EditorGUILayout.TextField((string)value)
            },
            {
                typeof(bool), value => EditorGUILayout.Toggle((bool)value)
            },
            {
                typeof(byte), value => (byte)EditorGUILayout.IntField((int)value)
            },
            {
                typeof(short), value => (short)EditorGUILayout.IntField((int)value)
            },
            {
                typeof(ushort),
                value =>
                    (ushort)Mathf.Max(EditorGUILayout.IntField(Convert.ToInt16((ushort)value)), 0)
            },
            {
                typeof(int), value => EditorGUILayout.IntField((int)value)
            },
            {
                typeof(uint),
                value =>
                    (uint)Mathf.Max(EditorGUILayout.IntField(Convert.ToInt32((uint)value)), 0)
            },
            {
                typeof(long), value => EditorGUILayout.LongField((long)value)
            },
            {
                typeof(ulong),
                value =>
                    (ulong)Mathf.Max(EditorGUILayout.LongField(Convert.ToInt64((ulong)value)), 0)
            },
            {
                typeof(float), value => EditorGUILayout.FloatField((float)value)
            },
            {
                typeof(double), value => EditorGUILayout.DoubleField((double)value)
            },
            {
                typeof(Color), value => EditorGUILayout.ColorField((Color)value)
            },
            {
                typeof(Vector2), value => EditorGUILayout.Vector2Field("", (Vector2)value)
            },
            {
                typeof(Vector2Int),
                value => EditorGUILayout.Vector2IntField("", (Vector2Int)value)
            },
            {
                typeof(Vector3), value => EditorGUILayout.Vector3Field("", (Vector3)value)
            },
            {
                typeof(Vector3Int),
                value => EditorGUILayout.Vector3IntField("", (Vector3Int)value)
            },
            {
                typeof(Vector4), value => EditorGUILayout.Vector4Field("", (Vector4)value)
            },
            {
                typeof(Rect), value => EditorGUILayout.RectField((Rect)value)
            },
            {
                typeof(RectInt), value => EditorGUILayout.RectIntField((RectInt)value)
            },
            {
                typeof(Quaternion), value =>
                {
                    var quaternion = (Quaternion)value;
                    var vec4 = EditorGUILayout.Vector4Field("",
                        new Vector4(quaternion.x, quaternion.y, quaternion.z, quaternion.w));
                    return new Quaternion(vec4.x, vec4.y, vec4.z, vec4.w);
                }
            }
        };

        public static void Register(string name, World world)
        {
            Worlds[name] = world;
            OnRegistered?.Invoke(name, world);
        }

        public static void Clear()
        {
            Worlds.Clear();
            OnRegistered = null;
        }
    }
}
#endif