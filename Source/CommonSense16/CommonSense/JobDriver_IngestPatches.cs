using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace CommonSense
{
    // Clean as a separate job. Ingest keeps its own targets, reservations and toils.
    static class JobDriver_IngestCleaning
    {
        internal static Job TryMakeCleaningJob(Pawn pawn, Job job)
        {
            if (!Settings.adv_cleaning_ingest || pawn.Drafted)
                return null;

            LocalTargetInfo target;
            if (job.def.driverClass == typeof(JobDriver_SocialRelax))
            {
                target = job.targetA;
            }
            else if (job.def.driverClass == typeof(JobDriver_Ingest))
            {
                Thing food = job.targetA.Thing;
                if (food == null)
                    return null;

                IntVec3 cell;
                if (!Toils_Ingest.TryFindChairOrSpot(pawn, food, out cell))
                    cell = pawn.Position;
                target = cell;
            }
            else
            {
                return null;
            }

            return MakeCleaningJob(pawn, target, Settings.adv_clean_num);
        }

        internal static Job MakeCleaningJob(Pawn pawn, LocalTargetInfo target, int limit)
        {
            if (!target.IsValid || Utility.IncapableOfCleaning(pawn))
                return null;

            var filth = Utility.SelectAllFilth(pawn, target, Math.Max(0, limit))
                .Where(f => f != null && !f.Destroyed).ToList();
            if (filth.Count == 0)
                return null;

            Job clean = JobMaker.MakeJob(JobDefOf.Clean);
            Utility.AddFilthToQueue(clean, TargetIndex.A, filth, pawn);
            return clean;
        }
    }
}
