using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using RimWorld;
using Verse;
using Verse.AI;

namespace CommonSense
{
    class WorkGiver_ClearSnow_Patch
    {
        [HarmonyPatch(typeof(WorkGiver_ClearSnow), "ShouldSkip")]
        static class WorkGiver_ClearSnow_ShouldSkip_CommonSensePatch
        {
            static bool Prefix(ref bool __result, Pawn pawn)
            {
                if (!Settings.skip_snow_clean || pawn.Map.weatherManager.SnowRate == 0 && pawn.Map.weatherManager.RainRate == 0)
                    return true;

                __result = false;
                return false;
            }
        }
    }
}
