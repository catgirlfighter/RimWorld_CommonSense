using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Reflection;

namespace CommonSense
{
    class OpportunisticTasks
    {
        private static WorkGiverDef haulGeneral = null;

        private static Job MakeCleaningJob(Pawn pawn, LocalTargetInfo target, int Limit)
        {
            if (Utility.IncapableOfCleaning(pawn))
                return null;

            IEnumerable<Filth> l = Utility.SelectAllFilth(pawn, target, Limit);

            Job job = new Job(JobDefOf.Clean);

            if (l.Count() == 0)
                return null;

            Utility.AddFilthToQueue(job, TargetIndex.A, l, pawn);

            return job;
        }

        public class Pawn_JobTracker_Crutch : Pawn_JobTracker
        {
            public Pawn_JobTracker_Crutch(Pawn newPawn) : base(newPawn)
            {
                pawn = newPawn;
            }

            public Pawn _pawn => base.pawn;
        }

        private static Job Cleaning_Opportunity(Job currJob, Pawn pawn, int Limit)
        {
            if (Utility.IncapableOfCleaning(pawn))
                return null;

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
                float stot;// = 0; //source to target
                float stob;// = 0; //source to building
                float btot;// = 0; //building to target
                bool b;// = false;
                if (Settings.calculate_full_path)
                {
                    PawnPath pawnPath = target.Map.pathFinder.FindPath(source, target, TraverseParms.For(TraverseMode.PassDoors, Danger.None), PathEndMode.Touch);
                    if (!pawnPath.Found)
                    {
                        pawnPath.ReleaseToPool();
                        return null;
                    }
                    stot = pawnPath.TotalCost;
                    pawnPath.ReleaseToPool();

                    pawnPath = building.Map.pathFinder.FindPath(source, building, TraverseParms.For(TraverseMode.PassDoors, Danger.None), PathEndMode.Touch);
                    if (!pawnPath.Found)
                    {
                        pawnPath.ReleaseToPool();
                        return null;
                    }
                    stob = pawnPath.TotalCost;
                    pawnPath.ReleaseToPool();

                    pawnPath = target.Map.pathFinder.FindPath(building.Position, target, TraverseParms.For(TraverseMode.PassDoors, Danger.None), PathEndMode.Touch);
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

        private static Job Hauling_Opportunity(Job billJob, Pawn pawn)
        {
            if (billJob.targetA != null && billJob.targetA.Thing != null && billJob.targetQueueB != null && billJob.targetQueueB.Count > 0)
            {

                float ptos = pawn.Position.DistanceToSquared(billJob.targetA.Cell);
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

                    foreach (var cap in haulGeneral.requiredCapacities)
                        if (!pawn.health.capacities.CapableOf(cap))
                            return null;

                    //Job job = null;
                    foreach (var target in (billJob.targetQueueB))
                        if (target.Thing != null && target.Thing.def.stackLimit > 1 && target.Thing.Map != null && (outdoors || target.Thing.GetRoom() != room))
                        {
                            var job = ((WorkGiver_Scanner)haulGeneral.Worker).JobOnThing(pawn, target.Thing);
                            if (job != null)
                                if(pawn.Position.DistanceToSquared(job.targetB.Cell) < ptos)
                                    return job;
                        }
                }
            }
            return null;
        }

        private static bool ProperJob(Job job, Pawn pawn, JobDef def)
        {
            return job?.def == def && job.targetA != null && job.targetA.Thing != null &&
                job.targetA.Thing != pawn;
        }

        [HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
        static class Pawn_JobTracker_StartJob_CommonSensePatch
        {
            internal static bool Prepare()
            {
                return !Settings.optimal_patching_in_use || Settings.fun_police || Settings.clean_before_work || Settings.hauling_over_bills;
            }
            internal static bool Prefix(ref Pawn_JobTracker_Crutch __instance, Job newJob, bool fromQueue)
            {
                try
                {
                    if (__instance?._pawn == null || !__instance._pawn.IsColonistPlayerControlled || newJob?.def == null 
                        || __instance.jobQueue == null || __instance.jobQueue.Count > 0)
                        return true;

                    if (Settings.fun_police && __instance._pawn.needs?.joy != null && __instance._pawn.needs.joy.CurLevel < 0.8f)
                    {
                        CompJoyToppedOff c = __instance._pawn.TryGetComp<CompJoyToppedOff>();
                        if (c != null)
                            c.JoyToppedOff = false;
                    }

                    if (!Settings.clean_before_work && !Settings.hauling_over_bills)
                        return true;

                    var oppClean = Settings.clean_before_work ? newJob.def.GetModExtension<CleanOnOpportunity>() : null;

                    if (!newJob.def.allowOpportunisticPrefix && oppClean?.doClean != true)
                        return true;

                    Job job = null;

                    if (newJob.def == JobDefOf.DoBill)
                    {
                        if (Settings.hauling_over_bills)
                        {
                            job = Hauling_Opportunity(newJob, __instance._pawn);
                        }
                    }
                    else if (!fromQueue && !newJob.playerForced && newJob.targetA != null && newJob.targetA.Cell != null)
                    {
                        IntVec3 cell = newJob.targetA.Cell;

                        if (!cell.IsValid || cell.IsForbidden(__instance._pawn) || __instance._pawn.Downed)
                        {
                            return true;
                        }
                        if (Settings.clean_before_work
                        && (oppClean?.doClean != false)
                        && (
                                oppClean?.doClean == true
                                || newJob.targetA.Thing != null && newJob.targetA.Thing.GetType().IsSubclassOf(typeof(Building)) && (!Settings.clean_gizmo || newJob.targetA.Thing.TryGetComp<DoCleanComp>()?.Active != false)
                                || newJob.def.joyKind != null
                        )
                        && !HealthAIUtility.ShouldBeTendedNowByPlayer(__instance._pawn))
                        {
                            job = Cleaning_Opportunity(newJob, __instance._pawn, Settings.op_clean_num);
                        }
                    }

                    if (job != null)
                    {
                        //if (Settings.add_to_que)
                        __instance.jobQueue.EnqueueFirst(newJob);
                        __instance.jobQueue.EnqueueFirst(job);
                        return false;
                    }
                }
                catch (Exception e)
                {
                    Log.Warning($"CommonSense: opportunistic task skipped due to error ({e.Message}) ({__instance._pawn}, {newJob})");
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Pawn_JobTracker), "EndCurrentJob")]
        static class Pawn_JobTracker_EndCurrentJob_CommonSensePatch
        {
            internal static bool Prepare()
            {
                return !Settings.optimal_patching_in_use || Settings.fun_police || Settings.clean_after_tending;
            }
            internal static bool Prefix(Pawn_JobTracker_Crutch __instance, JobCondition condition)
            {
                if (__instance == null || __instance.curJob == null || __instance._pawn == null  || !__instance._pawn.IsColonistPlayerControlled)
                    return true;

                if (Settings.fun_police && __instance._pawn.needs.joy != null && __instance._pawn.needs.joy.CurLevel > 0.95f)
                {
                    CompJoyToppedOff c = __instance._pawn.TryGetComp<CompJoyToppedOff>();
                    if (c != null)
                        c.JoyToppedOff = true;
                }

                Job job = null;
                if (Settings.clean_before_work && condition == JobCondition.Succeeded && __instance.jobQueue != null
                    && __instance.jobQueue.Count == 0 && __instance.curJob != null && ProperJob(__instance.curJob, __instance._pawn, JobDefOf.DeliverFood))
                {
                    job = MakeCleaningJob(__instance._pawn, __instance.curJob.targetA, Settings.op_clean_num);
                }

                if (Settings.clean_after_tending && condition == JobCondition.Succeeded && __instance.jobQueue != null
                    && __instance.jobQueue.Count == 0 && ProperJob(__instance.curJob, __instance._pawn, JobDefOf.TendPatient))
                {
                    ThinkTreeDef thinkTree = null;
                    MethodInfo mi = AccessTools.Method(typeof(Pawn_JobTracker), "DetermineNextJob");
                    ThinkResult thinkResult = (ThinkResult)mi.Invoke(__instance, new object[] { thinkTree, false });
                    if (ProperJob(thinkResult.Job, __instance._pawn, JobDefOf.TendPatient))
                    {
                        Pawn pawn = (Pawn)thinkResult.Job.targetA.Thing;
                        if (pawn.GetRoom() == __instance.curJob.targetA.Thing.GetRoom() || (HealthUtility.TicksUntilDeathDueToBloodLoss(pawn) / 2500f) < 6)
                            return true;
                    }
                    job = MakeCleaningJob(__instance._pawn, __instance.curJob.targetA, Settings.doc_clean_num);
                }
                //
                if (job != null)
                {
                    __instance.jobQueue.EnqueueFirst(job);
                }
                //
                return true;
            }
        }
    }
}
