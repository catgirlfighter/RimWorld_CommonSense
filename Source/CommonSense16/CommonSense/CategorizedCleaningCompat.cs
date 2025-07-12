using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonSense
{
    public static class CategorizedCleaningCompat
    {

        public delegate bool IsRoomType(Room room);
        public static IsRoomType method_IsSterileRoom;
        public static IsRoomType method_IsOutsideOrBarn;

        public static bool active = true;
        static WorkGiver_CleanFilth cleanFilthSterile;
        static WorkGiver_CleanFilth cleanFilthIndoors;

        static CategorizedCleaningCompat()
        {
            if (ModLister.GetActiveModWithIdentifier("PeteTimesSix.CategorizedCleaningSplit", true) != null || ModLister.GetActiveModWithIdentifier("PeteTimesSix.CategorizedCleaningCombo", true) != null)
            {
                active = true;
                method_IsSterileRoom = MethodDelegate<IsRoomType>(TypeByName("PeteTimesSix.CategorizedCleaning.Helpers").GetMethod("IsSterileRoom"));
                method_IsOutsideOrBarn = MethodDelegate<IsRoomType>(TypeByName("PeteTimesSix.CategorizedCleaning.Helpers").GetMethod("IsOutsideOrBarn"));
            }
        }

        public static WorkGiver_Scanner GetWorkGiver(Room room)
        {
            if (categorizedCleaningActive)
            {
                if (cleanFilthSterile == null)
                    cleanFilthSterile = DefDatabase<WorkGiverDef>.GetNamed("CategorizedCleaning_CleanFilth_Sterile").Worker as WorkGiver_CleanFilth;
                if (method_IsSterileRoom(room))
                    return cleanFilthSterile;

                if (cleanFilthIndoors == null)
                    cleanFilthIndoors = DefDatabase<WorkGiverDef>.GetNamed("CategorizedCleaning_CleanFilth_Indoors").Worker as WorkGiver_CleanFilth;
                if (!method_IsOutsideOrBarn(room))
                    return cleanFilthIndoors;
            }

            if (cleanFilth == null)
                cleanFilth = DefDatabase<WorkGiverDef>.GetNamed("CleanFilth");
            return cleanFilth.Worker as WorkGiver_Scanner;
        }
    }
}
