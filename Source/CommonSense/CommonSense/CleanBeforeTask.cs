using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using Harmony;
using UnityEngine;

namespace CommonSense
{
    class CleanBeforeTask
    {
        static private WorkGiverDef cleanFilth = null;
        
        static IEnumerable<Filth> SelectAllFilth(Pawn pawn, LocalTargetInfo target)
        {
            Room room = null;
            if (target.Thing == null)
                if (target.Cell == null)
                    Log.Error("Invalid target: cell or thing it must be");
                else
                    room = GridsUtility.GetRoom(target.Cell, pawn.Map);
            else
                room = target.Thing.GetRoom();

            if (room == null )
                return new List<Filth>();

            PathGrid pathGrid = pawn.Map.pathGrid;
            if (pathGrid == null)
                return new List<Filth>();

            if (cleanFilth == null)
                cleanFilth = DefDatabase<WorkGiverDef>.GetNamed("CleanFilth");

            if (cleanFilth.Worker == null)
                return new List<Filth>();

            IEnumerable<Filth> enumerable = null;
            if (room.IsHuge || room.CellCount > 160)
            {
                enumerable = new List<Filth>();
                for (int i = 0; i < 200; i++)
                {
                    IntVec3 intVec = target.Cell + GenRadial.RadialPattern[i];
                    if (intVec.InBounds(pawn.Map) && intVec.InAllowedArea(pawn) && intVec.GetRoom(pawn.Map) == room)
                        ((List<Filth>)enumerable).AddRange(intVec.GetThingList(pawn.Map).OfType<Filth>().Where(f => !f.Destroyed
                            && ((WorkGiver_Scanner)cleanFilth.Worker).HasJobOnThing(pawn, f)));
                }
            }
            else
                enumerable = room.ContainedAndAdjacentThings.OfType<Filth>().Where(delegate (Filth f)
                {
                    if (f == null || f.Destroyed || !f.Position.InAllowedArea(pawn) || !((WorkGiver_Scanner)cleanFilth.Worker).HasJobOnThing(pawn, f)) 
                        return false;

                    Room room2 = f.GetRoom();
                    if (room2 == null || room2 != room && !room2.IsDoorway)
                        return false;

                    return true;
                });
            return enumerable;
        }

        static private WorkTypeDef fCleaningDef = null;
        static private WorkTypeDef CleaningDef
        {
            get
            {
                if (fCleaningDef == null)
                {
                   fCleaningDef = DefDatabase<WorkTypeDef>.GetNamed("Cleaning");
                }
                return fCleaningDef;
            }
        }

        static Job MakeCleaningJob(Pawn pawn, LocalTargetInfo target)
        {
            if (pawn.def.race == null ||
                (int)pawn.def.race.intelligence < 2 ||
                pawn.Faction != Faction.OfPlayer ||
                //pawn.Drafted || 
                (int)pawn.RaceProps.intelligence < 2 ||
                //pawn.story.WorkTagIsDisabled(WorkTags.ManualDumb | WorkTags.Cleaning) ||
                pawn.InMentalState || pawn.IsBurning() ||
                pawn.workSettings == null || !pawn.workSettings.WorkIsActive(CleaningDef))
                return null;


            IEnumerable<Filth> l = SelectAllFilth(pawn, target);

            Job job = new Job(JobDefOf.Clean);

            if (l.Count() == 0)
                return null;

            foreach (Filth f in (l))
                job.AddQueuedTarget(TargetIndex.A, f);

            if (job.targetQueueA != null && job.targetQueueA.Count >= 5)
            {
                job.targetQueueA.SortBy((LocalTargetInfo targ) => targ.Cell.DistanceToSquared(pawn.Position));
            }

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
            static bool Prefix(ref Pawn_JobTracker_Crutch __instance, Job newJob, JobCondition lastJobEndCondition, ThinkNode jobGiver, bool resumeCurJobAfterwards, bool cancelBusyStances, ThinkTreeDef thinkTree, JobTag? tag, bool fromQueue)
            {
                if (!Settings.clean_before_work)
                    return true;

                IntVec3 cell = newJob.targetA.Cell;
                if (!newJob.def.allowOpportunisticPrefix ||
                    newJob.playerForced ||
                    !cell.IsValid || cell.IsForbidden(__instance._pawn) ||
                    newJob.targetA == null ||
                    __instance._pawn.Downed ||
                    (newJob.targetA.Thing == null || !newJob.targetA.Thing.GetType().IsSubclassOf(typeof(Building))) &&
                    (newJob.def.joyKind == null || newJob.targetA.Cell == null))
                {
                    return true;
                }

                Thing target = null;
                IntVec3 source = __instance._pawn.Position;

                Thing building = newJob.targetA.Thing;
                if (building != null)
                { 
                    if (newJob.targetB != null)
                        target = newJob.targetB.Thing;

                    if (target == null && newJob.targetQueueB != null && newJob.targetQueueB.Count > 0)
                        target = newJob.targetQueueB[0].Thing;
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
                        if(!pawnPath.Found)
                        {
                            pawnPath.ReleaseToPool();
                            return true;
                        }
                        stot =  pawnPath.TotalCost;
                        pawnPath.ReleaseToPool();

                        pawnPath = building.Map.pathFinder.FindPath(source, building, TraverseParms.For(TraverseMode.PassDoors, Danger.Some), PathEndMode.Touch);
                        if (!pawnPath.Found)
                        {
                            pawnPath.ReleaseToPool();
                            return true;
                        }
                        stob = pawnPath.TotalCost;
                        pawnPath.ReleaseToPool();

                        pawnPath = target.Map.pathFinder.FindPath(building.Position, target, TraverseParms.For(TraverseMode.PassDoors, Danger.Some), PathEndMode.Touch);
                        if (!pawnPath.Found)
                        {
                            pawnPath.ReleaseToPool();
                            return true;
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
                        return true;
                }

                Job job = MakeCleaningJob(__instance._pawn,newJob.targetA);

                if (job != null)
                {
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
                if (Settings.clean_after_tanding && condition == JobCondition.Succeeded && __instance != null && __instance.curJob != null &&
                    __instance.curJob.def.defName == "TendPatient" && __instance.jobQueue != null &&
                    __instance.jobQueue.Count == 0 && __instance.curJob.targetA != null && __instance.curJob.targetA.Thing != null && 
                    __instance.curJob.targetA.Thing != __instance._pawn)
                {
                    Job job = MakeCleaningJob(__instance._pawn, __instance.curJob.targetA);
                    if (job != null)
                        __instance.jobQueue.EnqueueFirst(job);
                }
                return true;
            }
        }

    }
}
