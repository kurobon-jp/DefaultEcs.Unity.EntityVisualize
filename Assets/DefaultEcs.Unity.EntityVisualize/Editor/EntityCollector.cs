using System;
using System.Collections.Generic;
using DefaultEcs.Unity.EntityVisualize.Editor.Models;
using UnityEngine;

namespace DefaultEcs.Unity.EntityVisualize.Editor
{
    /// <summary>
    /// The entity collector class
    /// </summary>
    public class EntityCollector : IObserver<object>
    {
        private World _world;
        private readonly List<Entity> _entities = new();

        public bool IsDirty { get; set; } = true;

        public void OnNext(object value)
        {
            IsDirty = true;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Bind(World world)
        {
            _world = world;
            IsDirty = true;
            if (_world == null) return;
            var type = typeof(World).Assembly.GetType("DefaultEcs.MessageBridge");
            Debug.Assert(type != null);
            var method = type.GetMethod("SetObserver");
            Debug.Assert(method != null);
            method.Invoke(null, new object[] { world, this });
        }

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