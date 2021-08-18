using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System;

namespace CommonSense
{
    public class JoyGiver_TakeDrug_Patient : JoyGiver_Ingest
    {
        private static List<ThingDef> takeableDrugs = new List<ThingDef>();

        protected override Thing BestIngestItem(Pawn pawn, Predicate<Thing> extraValidator)
        {
            if (pawn.drugs == null)
            {
                return null;
            }
            Predicate<Thing> predicate = (Thing t) => CanIngestForJoy(pawn, t) && (extraValidator == null || extraValidator(t)) && t.def.ingestible != null && t.def.ingestible.drugCategory != DrugCategory.None;
            ThingOwner<Thing> innerContainer = pawn.inventory.innerContainer;
            for (int i = 0; i < innerContainer.Count; i++)
            {
                if (predicate(innerContainer[i]))
                {
                    return innerContainer[i];
                }
            }
            //bool flag = false;
            //if (pawn.story != null && (pawn.story.traits.DegreeOfTrait(TraitDefOf.DrugDesire) > 0 || pawn.InMentalState))
            //{
            //    flag = true;
            //}
            takeableDrugs.Clear();
            DrugPolicy currentPolicy = pawn.drugs.CurrentPolicy;
            for (int j = 0; j < currentPolicy.Count; j++)
            {
                if (/*flag ||*/ currentPolicy[j].allowedForJoy)
                {
                    takeableDrugs.Add(currentPolicy[j].drug);
                }
            }
            //
            takeableDrugs.Shuffle();
            for (int k = 0; k < takeableDrugs.Count; k++)
            {
                List<Thing> list = pawn.Map.listerThings.ThingsOfDef(takeableDrugs[k]);
                if (list.Count > 0)
                {
                    Thing thing = GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map, list, PathEndMode.OnCell, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 9999f, predicate, null);
                    if (thing != null)
                    {
                        return thing;
                    }
                }
            }
            return null;
        }
    }

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
                || d.giverClass == typeof(JoyGiver_TakeDrug_Patient)
                || d.giverClass == typeof(JoyGiver_VisitSickPawn));
            //
            var chances = new DefMap<JoyGiverDef, float>();
            foreach (var d in defs)
            {
                if (d.Worker.CanBeGivenTo(pawn))
                    chances[d] = (d.giverClass == typeof(JoyGiver_TakeDrug_Patient) ? 4f : d.Worker.GetChance(sick)) * Mathf.Max(0.001f, Mathf.Pow(1f - sick.needs.joy.tolerances[d.joyKind], 5f));
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