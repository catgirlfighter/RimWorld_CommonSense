using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace CommonSense
{
    // Store mod's per-JobDriver data. Saving/loading done as part of the JobDriver's ExposeData() call.
    public class JobDriverData
    {
        public float cleaningWorkDone;
        public float totalCleaningWorkDone;
        public float totalCleaningWorkRequired;

        private static Dictionary< JobDriver, JobDriverData > dict = new Dictionary< JobDriver, JobDriverData >();

        public static JobDriverData Get(JobDriver driver)
        {
            JobDriverData data;
            if( dict.TryGetValue(driver, out data))
                return data;
            data = new JobDriverData();
            dict[ driver ] = data;
            return data;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref cleaningWorkDone, "CommonSense.cleaningWorkDone", 0f);
            Scribe_Values.Look(ref totalCleaningWorkDone, "CommonSense.totalCleaningWorkDone", 0f);
            Scribe_Values.Look(ref totalCleaningWorkRequired, "CommonSense.totalCleaningWorkRequired", 0f);
        }

        public static void ClearAll()
        {
            dict.Clear();
        }
    }

    // Clean the data when starting a different game.
    [HarmonyPatch(typeof(Game))]
    public static class Game_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(InitNewGame))]
        internal static void InitNewGame()
        {
            JobDriverData.ClearAll();
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(LoadGame))]
        internal static void LoadGame()
        {
            JobDriverData.ClearAll();
        }
    }
}
