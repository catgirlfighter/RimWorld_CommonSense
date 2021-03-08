using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace CommonSense
{
    [HarmonyPatch(typeof(WorkGiver_VisitSickPawn), "JobOnThing")]
    static class WorkGiver_VisitSickPawn_JobOnThing_CommonSensePatch
    {
        static bool Prefix(ref Job __result, Pawn pawn, Thing t, bool forced)
        {
            if (!Settings.give_sick_joy_drugs)
                return true;
            //
            Pawn sick = (Pawn)t;
            List<JoyGiverDef> defs = DefDatabase<JoyGiverDef>.AllDefsListForReading.FindAll(
                d => d.giverClass == typeof(JoyGiver_Ingest) 
                || d.giverClass == typeof(JoyGiver_TakeDrug)
                || d.giverClass == typeof(JoyGiver_VisitSickPawn));
            //
            var chances = new DefMap<JoyGiverDef, float>();
            foreach (var d in defs)
            {
                if (d.Worker.CanBeGivenTo(sick))
                    chances[d] = d.Worker.GetChance(sick) * Mathf.Max(0.001f, Mathf.Pow(1f - sick.needs.joy.tolerances[d.joyKind], 5f));
                else
                    chances[d] = 0f;
            }
            JoyGiverDef def = null;
            Job newJob = null;
            int counter = 0;
            while (counter < defs.Count && defs.TryRandomElementByWeight(d => chances[d], out def))
            {
                if (def.giverClass == typeof(JoyGiver_VisitSickPawn))
                    return true;
                //
                newJob = def.Worker.TryGiveJob(sick);
                if (newJob != null)
                {
                    __result = JobMaker.MakeJob(JobDefOf.FeedPatient);
                    __result.targetA = newJob.targetA;
                    __result.targetB = t;
                    __result.count = newJob.count;

                    return false;
                }
                chances[def] = 0f;
                counter++;
            }

            return true;
        }
    }
}