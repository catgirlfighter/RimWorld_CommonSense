using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace CommonSense
{
    // Port of Common Sense - Fix's separate-job cleaning implementation.
    [HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
    static class CleanBeforeTaskPatch
    {
        internal static bool Prepare()
        {
            return !Settings.optimal_patching_in_use || Settings.clean_before_work || Settings.adv_cleaning_ingest;
        }

        internal static bool Prefix(Pawn_JobTracker __instance, Pawn ___pawn, Job newJob,
            bool fromQueue, bool addToJobsThisTick, JobTag? tag)
        {
            try
            {
                Pawn pawn = ___pawn;
                if (__instance?.jobQueue == null || __instance.jobQueue.Count != 0
                    || pawn == null || !pawn.IsColonistPlayerControlled || pawn.Map == null || pawn.Downed
                    || newJob?.def == null || newJob.playerForced || fromQueue || !addToJobsThisTick
                    || newJob.def == JobDefOf.Clean || newJob.def == JobDefOf.DoBill)
                    return true;

                Job clean = TryMakeOpportunityCleaningJob(pawn, newJob)
                    ?? JobDriver_IngestCleaning.TryMakeCleaningJob(pawn, newJob);
                if (clean == null)
                    return true;

                // The queued jobs skip this prefix, preventing another insertion.
                __instance.jobQueue.EnqueueFirst(newJob, tag);
                __instance.jobQueue.EnqueueFirst(clean);
                return false;
            }
            catch (Exception e)
            {
                Log.ErrorOnce("CommonSense: clean-before job insertion failed; allowing the original job to start. " + e,
                    0xC1EA01);
                return true;
            }
        }

        private static Job TryMakeOpportunityCleaningJob(Pawn pawn, Job job)
        {
            if (!Settings.clean_before_work)
                return null;

            bool? doClean = job.def.GetModExtension<CleanOnOpportunity>()?.doClean;
            if ((!job.def.allowOpportunisticPrefix && doClean != true) || doClean == false)
                return null;

            Thing target = job.targetA.Thing;
            bool building = target is Building;
            if (doClean != true && job.def.joyKind == null && !building)
                return null;
            if (building && Settings.clean_gizmo && target.TryGetComp<DoCleanComp>()?.Active == false)
                return null;
            if (!job.targetA.IsValid || job.targetA.Cell.IsForbidden(pawn)
                || HealthAIUtility.ShouldBeTendedNowByPlayer(pawn) || ShouldSkipLongDetour(pawn, job))
                return null;

            return JobDriver_IngestCleaning.MakeCleaningJob(pawn, job.targetA, Settings.op_clean_num);
        }

        private static bool ShouldSkipLongDetour(Pawn pawn, Job job)
        {
            Thing work = job.targetA.Thing;
            Thing target = job.targetB.Thing;
            if (target == null && !job.targetQueueB.NullOrEmpty())
                target = job.targetQueueB[0].Thing;
            if (work == null || target == null)
                return false;

            float pawnToTarget = (float)Math.Sqrt(pawn.Position.DistanceToSquared(target.Position));
            float pawnToWork = (float)Math.Sqrt(pawn.Position.DistanceToSquared(work.Position));
            float workToTarget = (float)Math.Sqrt(work.Position.DistanceToSquared(target.Position));
            float total = pawnToWork + workToTarget;
            return pawnToWork > 10f && total > 0f && pawnToTarget / total < 0.7f;
        }
    }
}
