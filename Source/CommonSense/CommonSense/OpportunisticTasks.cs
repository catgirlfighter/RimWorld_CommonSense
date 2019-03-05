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
    class OpportunisticTasks
    {
        static private WorkGiverDef cleanFilth = null;
        static private WorkGiverDef haulGeneral = null;
        const byte largeRoomSize = 160;

        public static IEnumerable<Filth> SelectAllFilth(Pawn pawn, LocalTargetInfo target)
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
            if (room.IsHuge || room.CellCount > largeRoomSize)
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
                    //Log.Message(f.ToString() + "," + f.Destroyed.ToString()+","+ f.Position.InAllowedArea(pawn).ToString()+","+ ((WorkGiver_Scanner)cleanFilth.Worker).HasJobOnThing(pawn, f).ToString());
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

        public static void AddFilthToQueue(Job j, TargetIndex ind, IEnumerable<Filth> l, Pawn pawn)
        {
            //l = from d in l
            //    orderby 
            //    select d;

            //int n = Math.Abs(l.ElementAt(0).InteractionCell.DistanceToSquared(pawn.Position));
            //Filth d = l.ElementAt(0);
            //for (int i=1;i<l.Count(); i++)
            //{
                //if 
                //l.ElementAt(i).InteractionCell.DistanceToSquared(pawn.Position);
            //}

            foreach (Filth f in (l))
                j.AddQueuedTarget(ind,f);

            var q = j.GetTargetQueue(ind);

            if (q.Count > 0)
            {
                int n = q[0].Cell.DistanceToSquared(pawn.Position);
                int x = 0;
                int idx = 0;
                LocalTargetInfo out_of_all_things_they_didnt_add_a_simple_swap = null;
                for (int i = 1; i < l.Count(); i++)
                {
                    x = q[i].Cell.DistanceToSquared(pawn.Position);
                    if (Math.Abs(x) < Math.Abs(n))
                    {
                        n = x;
                        idx = i;
                    }
                }

                if (idx != 0)
                {
                    out_of_all_things_they_didnt_add_a_simple_swap = q[idx];
                    q[idx] = q[0];
                    q[0] = out_of_all_things_they_didnt_add_a_simple_swap;
                }

                for (int i = 0; i < q.Count()-1; i++)
                {
                    n = q[i].Cell.DistanceToSquared(q[i + 1].Cell);
                    idx = i + 1;
                    for (int c = i + 2; c < q.Count(); c++)
                    {
                        x = q[i].Cell.DistanceToSquared(q[c].Cell);
                        if (Math.Abs(x) < Math.Abs(n))
                        {
                            n = x;
                            idx = c;
                        }
                    }

                    if (idx != i + 1)
                    {
                        out_of_all_things_they_didnt_add_a_simple_swap = q[idx];
                        q[idx] = q[i + 1];
                        q[i + 1] = out_of_all_things_they_didnt_add_a_simple_swap;
                    }
                }
            }
            //q.SortBy((LocalTargetInfo targ) => targ.Cell.DistanceToSquared(pawn.Position));



            //if (j.GetTargetQueue(ind).Count >= 5)
            //{
            //    var q = j.GetTargetQueue(ind);
            //    q.SortBy((LocalTargetInfo targ) => targ.Cell.DistanceToSquared(pawn.Position));
            //    LocalTargetInfo t = q[0];
            //    for(iint)
            //}
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

            AddFilthToQueue(job,TargetIndex.A, l, pawn);

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
            static Job Cleaning_Opportunity(Job currJob, IntVec3 cell, Pawn pawn)
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

                return MakeCleaningJob(pawn, currJob.targetA);
            }

            static Job Hauling_Opportunity(Job billJob, Pawn pawn)
            {
                if (billJob.targetA != null && billJob.targetA.Thing != null && billJob.targetQueueB != null && billJob.targetQueueB.Count > 0)
                {

                    Room room = billJob.targetA.Thing.GetRoom();
                    if (room != null)
                    {
                        bool outdoors = room.PsychologicallyOutdoors || room.IsHuge || room.CellCount > largeRoomSize;
                        if (haulGeneral == null)
                        {
                            //compatibility with "pick up and houl"
                            haulGeneral = DefDatabase<WorkGiverDef>.GetNamed("HaulToInventory");
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
                if (!Settings.clean_before_work && !Settings.hauling_over_bills)
                    return true;

                if (__instance == null || __instance._pawn == null || newJob == null || newJob.def == null || !newJob.def.allowOpportunisticPrefix)
                    return true;

                Job job = null;

                if (newJob.def == JobDefOf.DoBill)
                { 
                    if (Settings.hauling_over_bills)
                    {
                        job = Hauling_Opportunity(newJob, __instance._pawn);
                    }
                    //else if (Settings.adv_cleaning)
                    //{
                    //    LocalTargetInfo A = newJob.targetA;
                    //    IEnumerable<Filth> l = SelectAllFilth(__instance._pawn, A);
                    //    AddFilthToQueue(newJob, TargetIndex.A, l, __instance._pawn);
                    //    newJob.targetQueueA.Add(A);
                    //}
                }
                else if (!newJob.playerForced && newJob.targetA != null && newJob.targetA.Cell != null)
                    {
                        IntVec3 cell = newJob.targetA.Cell;

                        if (!cell.IsValid || cell.IsForbidden(__instance._pawn) || __instance._pawn.Downed)
                        {
                            return true;
                        }

                        if (Settings.clean_before_work && (newJob.targetA.Thing != null && newJob.targetA.Thing.GetType().IsSubclassOf(typeof(Building)) || newJob.def.joyKind != null))
                            job = Cleaning_Opportunity(newJob, cell, __instance._pawn);
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
                if (Settings.clean_after_tanding && condition == JobCondition.Succeeded && __instance != null && __instance.curJob != null &&
                    __instance.curJob.def == JobDefOf.TendPatient && __instance.jobQueue != null &&
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
