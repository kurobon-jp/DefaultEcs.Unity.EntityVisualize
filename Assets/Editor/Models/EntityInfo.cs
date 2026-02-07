using System.Collections.Generic;
using DefaultEcs.Serialization;

namespace DefaultEcs.Unity.EntityVisualize.Editor.Models
{
    /// <summary>
    /// The entity info class
    /// </summary>
    public partial class EntityInfo : IComponentReader
    {
        public Entity Entity  { get; }
        
        /// <summary>
        /// Gets the value of the entity id
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the value of the components
        /// </summary>
        public List<ComponentInfo> Components { get; } = new();

        public EntityInfo(Entity entity)
        {
            Entity = entity;
            Id = entity.GetHashCode();
            entity.ReadAllComponents(this);
        }

        /// <summary>
        /// Returns the string
        /// </summary>
        /// <returns>The string</returns>
        public override string ToString()
        {
            return $"Entity id:{Id}";
        }

        public void OnRead<T>(in T component, in Entity componentOwner)
        {
            Components.Add(new ComponentInfo(typeof(T), component));
        }
    }
}