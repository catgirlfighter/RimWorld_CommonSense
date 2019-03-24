using System;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace CommonSense
{
    class OpportunisticTasks
    {
        static private WorkGiverDef haulGeneral = null;

        static Job MakeCleaningJob(Pawn pawn, LocalTargetInfo target, int Limit)
        {
            if (pawn.def.race == null ||
                (int)pawn.def.race.intelligence < 2 ||
                pawn.Faction != Faction.OfPlayer ||
                //pawn.Drafted || 
                (int)pawn.RaceProps.intelligence < 2 ||
                //pawn.story.WorkTagIsDisabled(WorkTags.ManualDumb | WorkTags.Cleaning) ||
                pawn.InMentalState || pawn.IsBurning() ||
                pawn.workSettings == null || !pawn.workSettings.WorkIsActive(Utility.CleaningDef))
                return null;


            IEnumerable<Filth> l = Utility.SelectAllFilth(pawn, target, Limit);

            Job job = new Job(JobDefOf.Clean);

            if (l.Count() == 0)
                return null;

            Utility.AddFilthToQueue(job,TargetIndex.A, l, pawn);

            return job;
        }

        public class Pawn_JobTracker_Crutch : Pawn_JobTracker
        {
            public Pawn_JobTracker_Crutch(Pawn newPawn) : base(newPawn)
            {
                pawn = newPawn;
            }

            public Pawn _pawn
            {
                get
                {
                    return this.pawn;
                }
            }
        }

        //public void StartJob(Job newJob, JobCondition lastJobEndCondition = JobCondition.None, ThinkNode jobGiver = null, bool resumeCurJobAfterwards = false, bool cancelBusyStances = true, ThinkTreeDef thinkTree = null, JobTag? tag = default(JobTag?), bool fromQueue = false)
        [HarmonyPatch(typeof(Pawn_JobTracker), "StartJob", new Type[] { typeof(Job), typeof(JobCondition), typeof(ThinkNode), typeof(bool), typeof(bool), typeof(ThinkTreeDef), typeof(JobTag), typeof(bool) })]
        static class Pawn_JobTracker_StartJob_CommonSensePatch
        {
            static Job Cleaning_Opportunity(Job currJob, IntVec3 cell, Pawn pawn, int Limit)
            {
                Thing target = null;
                IntVec3 source = pawn.Position;

                Thing building = currJob.targetA.Thing;
                if (building != null)
                {
                    if (currJob.targetB != null)
                        target = currJob.targetB.Thing;

                    if (target == null && currJob.targetQueueB != null && currJob.targetQueueB.Count > 0)
                        target = currJob.targetQueueB[0].Thing;
                }
                if (target != null)
                {
                    float stot = 0; //source to target
                    float stob = 0; //source to building
                    float btot = 0; //building to target
                    bool b = false;
                    if (Settings.calculate_full_path)
                    {
                        PawnPath pawnPath = target.Map.pathFinder.FindPath(source, target, TraverseParms.For(TraverseMode.PassDoors, Danger.Some), PathEndMode.Touch);
                        if (!pawnPath.Found)
                        {
                            pawnPath.ReleaseToPool();
                            return null;
                        }
                        stot = pawnPath.TotalCost;
                        pawnPath.ReleaseToPool();

                        pawnPath = building.Map.pathFinder.FindPath(source, building, TraverseParms.For(TraverseMode.PassDoors, Danger.Some), PathEndMode.Touch);
                        if (!pawnPath.Found)
                        {
                            pawnPath.ReleaseToPool();
                            return null;
                        }
                        stob = pawnPath.TotalCost;
                        pawnPath.ReleaseToPool();

                        pawnPath = target.Map.pathFinder.FindPath(building.Position, target, TraverseParms.For(TraverseMode.PassDoors, Danger.Some), PathEndMode.Touch);
                        if (!pawnPath.Found)
                        {
                            pawnPath.ReleaseToPool();
                            return null;
                        }
                        btot = pawnPath.TotalCost;
                        pawnPath.ReleaseToPool();

                        b = stob > 500 && stot / (stob + btot) < 0.7f;
                    }
                    else
                    {
                        stot = Mathf.Sqrt(source.DistanceToSquared(target.Position));
                        stob = Mathf.Sqrt(source.DistanceToSquared(building.Position));
                        btot = Mathf.Sqrt(building.Position.DistanceToSquared(target.Position));
                        b = stob > 10 && stot / (stob + btot) < 0.7f;
                    }
                    if (b)
                        return null;
                }

                return MakeCleaningJob(pawn, currJob.targetA, Limit);
            }

            static Job Hauling_Opportunity(Job billJob, Pawn pawn)
            {
                if (billJob.targetA != null && billJob.targetA.Thing != null && billJob.targetQueueB != null && billJob.targetQueueB.Count > 0)
                {

                    Room room = billJob.targetA.Thing.GetRoom();
                    if (room != null)
                    {
                        bool outdoors = room.PsychologicallyOutdoors || room.IsHuge || room.CellCount > Utility.largeRoomSize;
                        if (haulGeneral == null)
                        {
                            //compatibility with "pick up and houl"
                            haulGeneral = DefDatabase<WorkGiverDef>.GetNamedSilentFail("HaulToInventory");
                            if (haulGeneral == null)
                                haulGeneral = DefDatabase<WorkGiverDef>.GetNamed("HaulGeneral");
                        }

                        Job job = null;

                        foreach (var target in (billJob.targetQueueB))
                            if (target.Thing != null && target.Thing.def.stackLimit > 1 && target.Thing.Map != null && (outdoors || target.Thing.GetRoom() != room))
                            {
                                job = ((WorkGiver_Scanner)haulGeneral.Worker).JobOnThing(pawn, target.Thing);
                                if (job != null)
                                    return job;
                            }
                    }
                }
                return null;
            }

            static bool Prefix(ref Pawn_JobTracker_Crutch __instance, Job newJob, JobCondition lastJobEndCondition, ThinkNode jobGiver, bool resumeCurJobAfterwards, bool cancelBusyStances, ThinkTreeDef thinkTree, JobTag? tag, bool fromQueue)
            {
                if (__instance == null || __instance._pawn == null || newJob == null || newJob.def == null)
                    return true;

                if (Settings.fun_police && __instance._pawn.needs.joy != null && __instance._pawn.needs.joy.CurLevel < 0.8f)
                {
                    CompJoyToppedOff c = __instance._pawn.TryGetComp<CompJoyToppedOff>();
                    if (c != null)
                        c.JoyToppedOff = false;
                }

                if (!Settings.clean_before_work && !Settings.hauling_over_bills)
                    return true;

                if (!newJob.def.allowOpportunisticPrefix)
                    return true;

                Job job = null;

                if (newJob.def == JobDefOf.DoBill)
                { 
                    if (Settings.hauling_over_bills)
                    {
                        job = Hauling_Opportunity(newJob, __instance._pawn);
                    }
                }
                else if (!newJob.playerForced && newJob.targetA != null && newJob.targetA.Cell != null)
                    {
                        IntVec3 cell = newJob.targetA.Cell;

                        if (!cell.IsValid || cell.IsForbidden(__instance._pawn) || __instance._pawn.Downed)
                        {
                            return true;
                        }

                        if (Settings.clean_before_work && (newJob.targetA.Thing != null && newJob.targetA.Thing.GetType().IsSubclassOf(typeof(Building)) || newJob.def.joyKind != null))
                            job = Cleaning_Opportunity(newJob, cell, __instance._pawn, 20);
                    }

                if (job != null)
                {
                    if (Settings.add_to_que)
                        __instance.jobQueue.EnqueueFirst(newJob);
                    __instance.jobQueue.EnqueueFirst(job);
                    __instance.curJob = null;
                    __instance.curDriver = null;
                    return false;
                }
                return true;
            }
        }

        //public void EndCurrentJob(JobCondition condition, bool startNewJob = true)
        [HarmonyPatch(typeof(Pawn_JobTracker), "EndCurrentJob", new Type[] { typeof(JobCondition), typeof(bool) })]
        static class Pawn_JobTracker_EndCurrentJob_CommonSensePatch
        {
            static bool Prefix(Pawn_JobTracker_Crutch __instance, JobCondition condition, bool startNewJob)
            {
                if (__instance == null || __instance.curJob == null)
                    return true;

                if (Settings.fun_police && __instance._pawn.needs.joy != null && __instance._pawn.needs.joy.CurLevel > 0.95f)
                {
                    CompJoyToppedOff c = __instance._pawn.TryGetComp<CompJoyToppedOff>();
                    if (c != null)
                        c.JoyToppedOff = true;
                }

                if (Settings.clean_after_tanding && condition == JobCondition.Succeeded &&
                    __instance.curJob.def == JobDefOf.TendPatient && __instance.jobQueue != null &&
                    __instance.jobQueue.Count == 0 && __instance.curJob.targetA != null && __instance.curJob.targetA.Thing != null && 
                    __instance.curJob.targetA.Thing != __instance._pawn)
                {
                    Job job = MakeCleaningJob(__instance._pawn, __instance.curJob.targetA, int.MaxValue);
                    if (job != null)
                        __instance.jobQueue.EnqueueFirst(job);
                }
                return true;
            }
        }

    }
}
