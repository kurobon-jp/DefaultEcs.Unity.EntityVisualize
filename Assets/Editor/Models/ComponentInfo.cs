using System;

namespace DefaultEcs.Unity.EntityVisualize.Editor.Models
{
    /// <summary>
    /// The component info class
    /// </summary>
    public partial class ComponentInfo
    {
        /// <summary>
        /// Gets the value of the component
        /// </summary>
        public object Component { get; }

        /// <summary>
        /// Gets the value of the component type
        /// </summary>
        public Type ComponentType { get; }

        /// <summary>
        /// Gets the value of the component name
        /// </summary>
        public string ComponentName { get; }

        public ComponentInfo(Type componentType, object component)
        {
            Component = component;
            ComponentType = componentType;
            ComponentName = componentType.Name;
        }
    }
}