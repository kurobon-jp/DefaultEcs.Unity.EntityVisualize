using System.Collections.Generic;
using DefaultEcs.Unity.EntityVisualize.Editor.Models;

namespace DefaultEcs.Unity.EntityVisualize.Editor
{
    /// <summary>
    /// The entity collector class
    /// </summary>
    public class EntityCollector
    {
        private World _world;
        private readonly List<Entity> _entities = new();

        public bool IsDirty { get; set; } = true;

        public void Bind(World world)
        {
            _world = world;
            IsDirty = true;
            if (_world == null) return;
            Internal.Publisher.OnPublish -= OnWorldChanged;
            Internal.Publisher.OnPublish += OnWorldChanged;
        }

        private void OnWorldChanged(int worldId, object message)
        {
            if (_world == null || worldId != _world.WorldId || message is Internal.Message.ComponentReadMessage) return;
            IsDirty = true;
        }

        /// <summary>
        /// Ticks this instance
        /// </summary>
        public List<Entity> CollectEntities()
        {
            _entities.Clear();
            if (_world == null) return _entities;
            foreach (var entity in _world)
            {
                if (!entity.IsAlive) continue;
                _entities.Add(entity);
            }

            return _entities;
        }

        public EntityInfo GetEntityInfo(Entity entity)
        {
            if (!entity.IsAlive) return null;
            return new EntityInfo(entity);
        }
    }
}