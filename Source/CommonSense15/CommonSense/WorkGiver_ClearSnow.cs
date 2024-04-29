using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace CommonSense
{
    public static class WorkGiver_ClearSnow_Patch
    {
        [HarmonyPatch(typeof(WorkGiver_ClearSnow), "ShouldSkip")]
        public static class WorkGiver_ClearSnow_ShouldSkip_CommonSensePatch
        {
            internal static bool Prepare()
            {
                return !Settings.optimal_patching_in_use || Settings.skip_snow_clean;
            }
            public static bool Prefix(ref bool __result, Pawn pawn)
            {
                if (!Settings.skip_snow_clean || pawn.Map.weatherManager.SnowRate == 0 && pawn.Map.weatherManager.RainRate == 0)
                    return true;

                __result = true;
                return false;
            }
        }
    }
}
