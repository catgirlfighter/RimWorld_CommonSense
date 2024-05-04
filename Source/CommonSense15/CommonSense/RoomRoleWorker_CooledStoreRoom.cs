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
    public class RoomRoleWorker_CooledStoreRoom : RoomRoleWorker
    {
        // Token: 0x06006A54 RID: 27220 RVA: 0x0023D334 File Offset: 0x0023B534
        private bool IsFridgeCooler(Building_Cooler cooler, Room room)
        {
            var vec = cooler.Position + IntVec3.South.RotatedBy(cooler.Rotation);
            return cooler.compTempControl.targetTemperature < 0f
                && !vec.Impassable(cooler.Map)
                && vec.GetRoom(cooler.Map) == room;
        }
        
        public override float GetScore(Room room)
        {
            int num = 0;
            List<Thing> containedAndAdjacentThings = room.ContainedAndAdjacentThings;
            for (int i = 0; i < containedAndAdjacentThings.Count; i++)
            {
                if (containedAndAdjacentThings[i] is Building_Storage)
                {
                    num++;
                }
                if (containedAndAdjacentThings[i] is Building_Cooler cooler && IsFridgeCooler(cooler, room))
                {
                    num += 10;
                }
            }
            return 3f * num - 1f;
        }
    }
}
