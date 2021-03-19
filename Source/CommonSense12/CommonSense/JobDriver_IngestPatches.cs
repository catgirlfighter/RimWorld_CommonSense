
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Reflection;

namespace CommonSense
{
    [HarmonyPatch(typeof(JobDriver_Ingest), "MakeNewToils")]
    static class JobDriver_Ingest_MakeNewToils_CommonSensePatch
    {
        static FieldInfo LusingNutrientPasteDispenser = null;
        static PropertyInfo LChewDurationMultiplier = null;
        static MethodInfo LPrepareToIngestToils = null;

        static void Prepare()
        {
            LusingNutrientPasteDispenser = AccessTools.Field(typeof(JobDriver_Ingest), "usingNutrientPasteDispenser");
            LChewDurationMultiplier = AccessTools.Property(typeof(JobDriver_Ingest), "ChewDurationMultiplier");
            LPrepareToIngestToils = AccessTools.Method(typeof(JobDriver_Ingest), "PrepareToIngestToils");
        }

        static Thing IngestibleSource(this JobDriver_Ingest driver)
        {
            return driver.job.GetTarget(TargetIndex.A).Thing;
        }

        static float ChewDurationMultiplier(this JobDriver_Ingest driver)
        {
            return (float)LChewDurationMultiplier.GetValue(driver);
        }

        static bool usingNutrientPasteDispenser(this JobDriver_Ingest driver)
        {
            return (bool)LusingNutrientPasteDispenser.GetValue(driver);
        }

        static IEnumerable<Toil> PrepareToIngestToils(this JobDriver_Ingest driver, Toil chewToil)
        {
            return (IEnumerable<Toil>)LPrepareToIngestToils.Invoke(driver, new object[] { chewToil });
        }

        static IEnumerable<Toil> _MakeToils(this JobDriver_Ingest driver)
        {
            if (!driver.usingNutrientPasteDispenser())
            {
                driver.FailOn(() => !driver.IngestibleSource().Destroyed && !driver.IngestibleSource().IngestibleNow);
            }

            Toil chew = Toils_Ingest.ChewIngestible(driver.pawn, driver.ChewDurationMultiplier(), TargetIndex.A, TargetIndex.B).FailOn((Toil x) => !driver.IngestibleSource().Spawned && (driver.pawn.carryTracker == null || driver.pawn.carryTracker.CarriedThing != driver.IngestibleSource())).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);

            Toil nextstep;

            TargetIndex filthListIndex = TargetIndex.B;
            TargetIndex progIndex = TargetIndex.A;
            if (Settings.adv_cleaning_ingest && !Utility.IncapableOfCleaning(driver.pawn))
            {
                Toil FilthList = new Toil();
                FilthList.initAction = delegate ()
                {
                    Job curJob = FilthList.actor.jobs.curJob;
                    //
                    if (curJob.GetTargetQueue(filthListIndex).NullOrEmpty())
                    {
                        LocalTargetInfo A = curJob.GetTarget(filthListIndex);
                        IEnumerable<Filth> l = Utility.SelectAllFilth(FilthList.actor, A, Settings.adv_clean_num);
                        Utility.AddFilthToQueue(curJob, filthListIndex, l, FilthList.actor);
                        FilthList.actor.ReserveAsManyAsPossible(curJob.GetTargetQueue(filthListIndex), curJob);
                        curJob.targetQueueA.Add(A);
                    }
                };
                //
                nextstep = FilthList;
            }
            else
                nextstep = chew;

            foreach (Toil toil in driver.PrepareToIngestToils(nextstep))
            {
                yield return toil;
            }
            //IEnumerator<Toil> enumerator = null;
            if (nextstep != chew)
            {
                yield return Toils_Jump.JumpIf(chew, () => !(driver.pawn.Position + driver.pawn.Rotation.FacingCell).HasEatSurface(driver.pawn.Map));
                Toil toil = new Toil();
                toil.initAction = delegate () { driver.job.SetTarget(TargetIndex.C, driver.pawn.Position); };
                yield return toil;
                
                Toil GoToPlace = Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell);
                yield return nextstep;
                yield return Toils_Jump.JumpIf(GoToPlace, () => driver.job.GetTargetQueue(filthListIndex).NullOrEmpty());
                Toil CleanFilthList = Toils_JobTransforms.ClearDespawnedNullOrForbiddenQueuedTargets(filthListIndex, null);
                yield return CleanFilthList;
                yield return Toils_JobTransforms.ExtractNextTargetFromQueue(filthListIndex, true);
                yield return Toils_Jump.JumpIf(GoToPlace, () => driver.job.GetTargetQueue(filthListIndex).NullOrEmpty());
                yield return Toils_Goto.GotoThing(filthListIndex, PathEndMode.Touch).JumpIfDespawnedOrNullOrForbidden(filthListIndex, CleanFilthList).JumpIfOutsideHomeArea(filthListIndex, CleanFilthList);
                //
                driver.job.GetTargetQueue(progIndex).Add(new IntVec3(0, 0, 0));
                Toil clean = new Toil();
                clean.initAction = delegate ()
                {
                    Filth filth = clean.actor.jobs.curJob.GetTarget(filthListIndex).Thing as Filth;
                    var progQue = clean.actor.jobs.curJob.GetTargetQueue(progIndex);
                    //x = billStartTick, y = ticksSpentDoingRecipeWork, z = workLeft;
                    progQue[0] = new IntVec3(0, 0, (int)filth.def.filth.cleaningWorkToReduceThickness * filth.thickness);
                    //driver.billStartTick = 0;
                    //driver.ticksSpentDoingRecipeWork = 0;
                    //driver.workLeft = filth.def.filth.cleaningWorkToReduceThickness * filth.thickness;
                };
                clean.tickAction = delegate ()
                {
                    Filth filth = clean.actor.jobs.curJob.GetTarget(filthListIndex).Thing as Filth;
                    var progQue = clean.actor.jobs.curJob.GetTargetQueue(progIndex);
                    //driver.billStartTick += 1;
                    //driver.ticksSpentDoingRecipeWork += 1;
                    IntVec3 iv = progQue[0].Cell;
                    iv.x += 1;
                    iv.y += 1;
                    if (iv.x > filth.def.filth.cleaningWorkToReduceThickness)
                    {
                        filth.ThinFilth();
                        iv.x = 0;
                        progQue[0] = iv;
                        if (filth.Destroyed)
                        {
                            clean.actor.records.Increment(RecordDefOf.MessesCleaned);
                            driver.ReadyForNextToil();
                            return;
                        }
                    }
                    progQue[0] = iv;
                };
                clean.defaultCompleteMode = ToilCompleteMode.Never;
                clean.WithEffect(EffecterDefOf.Clean, filthListIndex);
                clean.WithProgressBar(filthListIndex, () => driver.job.GetTargetQueue(progIndex)[0].Cell.y / driver.job.GetTargetQueue(progIndex)[0].Cell.z, true, -0.5f);
                clean.PlaySustainerOrSound(() => SoundDefOf.Interact_CleanFilth);
                clean.JumpIfDespawnedOrNullOrForbidden(filthListIndex, nextstep);
                clean.JumpIfOutsideHomeArea(filthListIndex, nextstep);
                yield return clean;
                yield return Toils_Jump.Jump(nextstep);
                yield return GoToPlace;
                yield return Toils_Ingest.FindAdjacentEatSurface(TargetIndex.B, TargetIndex.A);
            }

            yield return chew;
            yield return Toils_Ingest.FinalizeIngest(driver.pawn, TargetIndex.A);
            yield return Toils_Jump.JumpIf(chew, () => driver.job.GetTarget(TargetIndex.A).Thing is Corpse && driver.pawn.needs.food.CurLevelPercentage < 0.9f);
            yield break;
            //yield break;
        }

        static bool Prefix(ref IEnumerable<Toil> __result, JobDriver_Ingest __instance)
        {
            if (!Settings.adv_cleaning_ingest)
                return true;
            //
            __result = __instance._MakeToils();
            return false;
        }
    }
}
