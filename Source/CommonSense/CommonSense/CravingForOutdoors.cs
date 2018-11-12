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
    class CravingForOutdoors
    {
        //RimWorld.JobGiver_GetJoy.TryGiveJob
        //protected override Job TryGiveJob(Pawn pawn)
        [HarmonyPatch(typeof(RimWorld.JobGiver_GetJoy), "TryGiveJob", new Type[] { typeof(Pawn) })]
        static class JobGiver_GetJoy_TryGiveJob_CommonSensePatch
        {
            public class JobCrutch: JobGiver_GetJoy
            {
                public bool CanDoDuringMedicalRestCrutch()
                {
                    return CanDoDuringMedicalRest;
                }
                public bool JoyGiverAllowedCrutch(JoyGiverDef def)
                {
                    return JoyGiverAllowed(def);
                }
                public Job TryGiveJobFromJoyGiverDefDirectCrutch(JoyGiverDef def, Pawn pawn)
                {
                    return TryGiveJobFromJoyGiverDefDirect(def, pawn);
                }
            }
            //Don't bother thinking about what to do if you're starved for one of the things
            static bool Prefix(ref Job __result, ref JobCrutch __instance,  ref Pawn pawn)
            {
                
                if (!Settings.fulfill_outdoors || !__instance.CanDoDuringMedicalRestCrutch() && pawn.InBed() && HealthAIUtility.ShouldSeekMedicalRest(pawn)
                    || pawn.needs.outdoors == null || pawn.needs.outdoors.CurLevel >= 0.4f)
                {
                    return true;
                }


                List<JoyGiverDef> l = DefDatabase<JoyGiverDef>.AllDefsListForReading.FindAll(x => x.unroofedOnly);
                Random rand = new Random();
                int n = rand.Next(0,l.Count)+1;

                for(int i = 0; i < l.Count; i++)
                {
                    JoyGiverDef jgd = l[(n + i) % l.Count];
                    if (!__instance.JoyGiverAllowedCrutch(jgd) || pawn.needs.joy.tolerances.BoredOf(jgd.joyKind) || jgd.Worker.MissingRequiredCapacity(pawn) != null)
                        continue;

                    Job job = __instance.TryGiveJobFromJoyGiverDefDirectCrutch(jgd, pawn);
                    if (job != null)
                    {
                        __result = job;
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
