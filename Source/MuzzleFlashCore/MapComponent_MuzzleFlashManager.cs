using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Verse;
using UnityEngine;
using RimWorld.Planet;

namespace MuzzleFlash
{
    public class MapComponent_MuzzleFlashManager : MapComponent
    {
        //private readonly LinkedList<MuzzleFlashEntity> _entities;
        private readonly List<MuzzleFlashEntity> _entities;
        public MapComponent_MuzzleFlashManager(Map map) : base(map)
        {
            _entities = new List<MuzzleFlashEntity>();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            //remove dead entities, 
            //just replace the dead entity with the last entity and remove the last entity
            int length = _entities.Count;
            int i = 0;
            while (i < length)
            {
                var entity = _entities[i];
                if (!entity.IsAlive)
                {
                    _entities[i] = _entities[length - 1];
                    _entities.RemoveAt(length - 1);
                    length--;
                }
                else
                {
                    entity.Tick();
                    _entities[i] = entity;//it a struct, so we need to replace it
                    i++;
                }
            }
        }

        public override void MapComponentUpdate()
        {
            if (WorldRendererUtility.WorldRendered || Find.CurrentMap != this.map) return;

            // var pointer = _entities.First;
            // while (pointer != null)
            // {
            //     var entity = pointer.Value;
            //     AnimatedRenderManager.Default.AddInstance(entity.def.RenderID, entity.position, entity.rotation, entity.size, Color.white, entity.frame);
            //     pointer = pointer.Next;
            // }
            for (int i = 0; i < _entities.Count; i++)
            {
                var entity = _entities[i];
                AnimatedRenderManager.Default.AddInstance(entity.def.RenderID, entity.position, entity.rotation, entity.size, Color.white, entity.frame);
            }
            AnimatedRenderManager.Default.Draw();
        }

        public void RegisterEntity(MuzzleFlashEntity entity)
        {
            _entities.Add(entity);
        }
    }
}
