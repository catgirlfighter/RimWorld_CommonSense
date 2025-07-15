using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace CommonSense
{
    [DefOf]
    public static class CSRoomRoleDefOf
    {
        public static RoomRoleDef CooledStoreroom;
        [MayRequireAnomaly]
        public static RoomRoleDef ContainmentCell;
    }
    public class RoomRoleWorker_CooledStoreRoom : RoomRoleWorker_StoreRoom
    {
        private bool IsFridgeCooler(Building_Cooler cooler, Room room)
        {
            IntVec3 vec;
            return cooler?.compTempControl?.targetTemperature < 0f
                && !(vec = cooler.Position + IntVec3.South.RotatedBy(cooler.Rotation)).Impassable(cooler.Map)
                && vec.GetRoom(cooler.Map) == room;
            
        }
        
        public override float GetScore(Room room)
        {
            var num = base.GetScore(room);
            if (num == 0f) return num;
            //int num0 = 0;
            //int num = 0;
            List<Thing> containedAndAdjacentThings = room.ContainedAndAdjacentThings;
            for (int i = 0; i < containedAndAdjacentThings.Count; i++)
            {
                if (containedAndAdjacentThings[i] is Building_Cooler cooler && IsFridgeCooler(cooler, room))
                {
                    num++;
                }
            }
            return num;
        }
    }
}
