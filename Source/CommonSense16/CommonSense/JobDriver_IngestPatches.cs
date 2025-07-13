using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Reflection;
using UnityEngine;

namespace CommonSense
{
    [HarmonyPatch(typeof(JobDriver_Ingest), "PrepareToIngestToils_ToolUser")]
    static class JobDriver_PrepareToIngestToils_ToolUser_CommonSensePatch
    {
        static FieldInfo LeatingFromInventory = null;
        static MethodInfo LReserveFood = null;
        static MethodInfo LTakeExtraIngestibles = null;

        internal static bool Prepare()
        {
            LeatingFromInventory = AccessTools.Field(typeof(JobDriver_Ingest), "eatingFromInventory");
            if (LeatingFromInventory == null) Log.Message("couldn't find field JobDriver_Ingest.eatingFromInventory");
            LReserveFood = AccessTools.Method(typeof(JobDriver_Ingest), "ReserveFood");
            if (LReserveFood == null) Log.Message("couldn't find method JobDriver_Ingest.ReserveFood");
            LTakeExtraIngestibles = AccessTools.Method(typeof(JobDriver_Ingest), "TakeExtraIngestibles");
            if (LTakeExtraIngestibles == null) Log.Message("couldn't find method JobDriver_Ingest.TakeExtraIngestibles");
            return LeatingFromInventory != null
                && LReserveFood != null
                && LTakeExtraIngestibles != null
                && (!Settings.optimal_patching_in_use || Settings.adv_cleaning_ingest);
        }

        public static Toil MakeFilthListToil(TargetIndex targetIndex)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Job curJob = toil.actor.jobs.curJob;
                //
                if (curJob.GetTargetQueue(targetIndex).NullOrEmpty())
                {
                    LocalTargetInfo filthListTarget = curJob.GetTarget(targetIndex);
                    if (!filthListTarget.IsValid) return;
                    IEnumerable<Filth> l = Utility.SelectAllFilth(toil.actor, filthListTarget, Settings.adv_clean_num);
                    Utility.AddFilthToQueue(curJob, targetIndex, l, toil.actor);
                    toil.actor.ReserveAsManyAsPossible(curJob.GetTargetQueue(targetIndex), curJob);
                    curJob.GetTargetQueue(targetIndex).Add(filthListTarget);
                }
            };
            return toil;
        }

        public static Toil MakeCleanToil(JobDriver_Ingest driver, TargetIndex progListIndex, TargetIndex filthListIndex, Toil nextTarget)
        {
            Toil toil = new Toil();
            JobDriverData data = JobDriverData.Get(driver);
            toil.initAction = delegate ()
            {
                Filth filth = toil.actor.jobs.curJob.GetTarget(filthListIndex).Thing as Filth;
                data.cleaningWorkDone = 0f;
                data.totalCleaningWorkDone = 0f;
                data.totalCleaningWorkRequired = filth.def.filth.cleaningWorkToReduceThickness * (float)filth.thickness;
            };
            toil.tickAction = delegate ()
            {
                Filth filth = toil.actor.jobs.curJob.GetTarget(filthListIndex).Thing as Filth;
                //
                float statValueAbstract = filth.Position.GetTerrain(filth.Map).GetStatValueAbstract(StatDefOf.CleaningTimeFactor, null);
                float num = toil.actor.GetStatValue(StatDefOf.CleaningSpeed, true, -1);
                if (statValueAbstract != 0f)
                {
                    num /= statValueAbstract;
                }
                //
                data.cleaningWorkDone += num;
                data.totalCleaningWorkDone += num;
                if (data.cleaningWorkDone > filth.def.filth.cleaningWorkToReduceThickness)
                {
                    filth.ThinFilth();
                    data.cleaningWorkDone = 0;
                    if (filth.Destroyed)
                    {
                        toil.actor.records.Increment(RecordDefOf.MessesCleaned);
                        toil.actor.jobs.curDriver.ReadyForNextToil();
                        return;
                    }
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithEffect(EffecterDefOf.Clean, filthListIndex);
            toil.WithProgressBar(filthListIndex,
                delegate ()
                {
                    return data.totalCleaningWorkDone / data.totalCleaningWorkRequired;
                }
                , true, -0.5f);
            toil.PlaySustainerOrSound(() => SoundDefOf.Interact_CleanFilth);
            toil.JumpIfDespawnedOrNullOrForbidden(filthListIndex, nextTarget);
            toil.JumpIfOutsideHomeArea(filthListIndex, nextTarget);
            toil.FailOnDestroyedOrNull(TargetIndex.A);
            return toil;
        }

        private static IEnumerable<Toil> PrepToils(JobDriver_Ingest driver, Toil chewToil)
        {
            if ((bool)LeatingFromInventory.GetValue(driver))
            {
                yield return Toils_Misc.TakeItemFromInventoryToCarrier(driver.pawn, TargetIndex.A);
            }
            else
            {
                yield return (Toil)LReserveFood.Invoke(driver, new object[] { });
                Toil gotoToPickup = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
                yield return Toils_Jump.JumpIf(gotoToPickup, () => driver.pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation));
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
                yield return Toils_Jump.Jump(chewToil);
                yield return gotoToPickup;
                yield return Toils_Ingest.PickupIngestible(TargetIndex.A, driver.pawn);
                gotoToPickup = null;
            }
            if (driver.job.takeExtraIngestibles > 0)
            {
                foreach (Toil toil in (IEnumerable<Toil>)LTakeExtraIngestibles.Invoke(driver, new object[] { }))
                {
                    yield return toil;
                }
            }
            if (!driver.pawn.Drafted)
            {
                yield return ReserveChewSpot(TargetIndex.A, TargetIndex.B);
                Toil gotospot = GotoSpot(TargetIndex.B).FailOnDestroyedOrNull(TargetIndex.A);

                if (!Utility.IncapableOfCleaning(driver.pawn))
                {
                    TargetIndex filthListIndex = TargetIndex.B;
                    TargetIndex progListIndex = TargetIndex.A;
                    Toil FilthList = MakeFilthListToil(filthListIndex);
                    yield return FilthList;
                    yield return Toils_Jump.JumpIf(gotospot, () => driver.job.GetTargetQueue(filthListIndex).NullOrEmpty());
                    Toil nextTarget = Toils_JobTransforms.ExtractNextTargetFromQueue(filthListIndex, true);
                    yield return nextTarget;
                    yield return Toils_Jump.JumpIf(gotospot, () => driver.job.GetTargetQueue(filthListIndex).NullOrEmpty());
                    yield return Toils_Goto.GotoThing(filthListIndex, PathEndMode.Touch).JumpIfDespawnedOrNullOrForbidden(filthListIndex, nextTarget).JumpIfOutsideHomeArea(filthListIndex, nextTarget);
                    //
                    if (driver.job.GetTargetQueue(progListIndex).Count == 0)
                        driver.job.GetTargetQueue(progListIndex).Add(new IntVec3(0, 0, 0));
                    //
                    Toil clean = MakeCleanToil(driver, progListIndex, filthListIndex, nextTarget);
                    yield return clean;
                    yield return Toils_Jump.Jump(nextTarget);
                }
                yield return gotospot;
            }
            yield return Toils_Ingest.FindAdjacentEatSurface(TargetIndex.B, TargetIndex.A);
            yield break;
        }

        public static Toil ReserveChewSpot(TargetIndex ingestibleInd, TargetIndex StoreToInd)
        {
            Toil toil = ToilMaker.MakeToil("CommonSense.ReserveChewSpot");
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                Thing thing = actor.CurJob.GetTarget(ingestibleInd).Thing;
                if (!Toils_Ingest.TryFindChairOrSpot(actor, thing, out var cell))
                {
                    Log.WarningOnce($"Can't find valid chair or spot for {actor} trying to ingest {thing}", HashCode.Combine(actor, thing));
                    actor.CurJob.SetTarget(StoreToInd, actor.Position);
                }
                else
                {
                    actor.ReserveSittableOrSpot(cell, actor.CurJob);
                    actor.Map.pawnDestinationReservationManager.Reserve(actor, actor.CurJob, cell);
                    actor.CurJob.SetTarget(StoreToInd, cell);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        public static Toil GotoSpot(TargetIndex gotoInd)
        {
            return Toils_Goto.GotoCell(gotoInd, PathEndMode.OnCell);
        }

        internal static bool Prefix(ref IEnumerable<Toil> __result, JobDriver_Ingest __instance, Toil chewToil)
        {
            if (!Settings.adv_cleaning_ingest)
                return true;
            //
            __result = PrepToils(__instance, chewToil);

            return false;
        }
    }

    [HarmonyPatch(typeof(JobDriver_Ingest), "PrepareToIngestToils_Dispenser")]
    static class JobDriver_PrepareToIngestToils_Dispenser_CommonSensePatch
    {
        internal static bool Prepare()
        {
            return !Settings.optimal_patching_in_use || Settings.adv_cleaning_ingest;
        }

        private static IEnumerable<Toil> PrepToils(JobDriver_Ingest driver)
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell).FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Ingest.TakeMealFromDispenser(TargetIndex.A, driver.pawn);
            if (!driver.pawn.Drafted)
                yield return JobDriver_PrepareToIngestToils_ToolUser_CommonSensePatch.ReserveChewSpot(TargetIndex.A, TargetIndex.B);
                Toil gotospot = JobDriver_PrepareToIngestToils_ToolUser_CommonSensePatch.GotoSpot(TargetIndex.B).FailOnDestroyedOrNull(TargetIndex.A);

                if (!Utility.IncapableOfCleaning(driver.pawn))
                {
                    TargetIndex filthListIndex = TargetIndex.B;
                    TargetIndex progListIndex = TargetIndex.A;
                    Toil FilthList = JobDriver_PrepareToIngestToils_ToolUser_CommonSensePatch.MakeFilthListToil(filthListIndex);
                    yield return FilthList;
                    yield return Toils_Jump.JumpIf(gotospot, () => driver.job.GetTargetQueue(filthListIndex).NullOrEmpty());
                    Toil nextTarget = Toils_JobTransforms.ExtractNextTargetFromQueue(filthListIndex, true);
                    yield return nextTarget;
                    yield return Toils_Jump.JumpIf(gotospot, () => driver.job.GetTargetQueue(filthListIndex).NullOrEmpty());
                    yield return Toils_Goto.GotoThing(filthListIndex, PathEndMode.Touch).JumpIfDespawnedOrNullOrForbidden(filthListIndex, nextTarget).JumpIfOutsideHomeArea(filthListIndex, nextTarget);
                    //
                    if (driver.job.GetTargetQueue(progListIndex).Count == 0)
                        driver.job.GetTargetQueue(progListIndex).Add(new IntVec3(0, 0, 0));
                    //
                    Toil clean = JobDriver_PrepareToIngestToils_ToolUser_CommonSensePatch.MakeCleanToil(driver, progListIndex, filthListIndex, nextTarget);
                    yield return clean;
                    yield return Toils_Jump.Jump(nextTarget);
                }
                yield return gotospot;

            yield return Toils_Ingest.FindAdjacentEatSurface(TargetIndex.B, TargetIndex.A);
        }

        internal static bool Prefix(ref IEnumerable<Toil> __result, JobDriver_Ingest __instance)
        {
            if (!Settings.adv_cleaning_ingest)
                return true;
            //
            __result = PrepToils(__instance);

            return false;
        }
    }

    [HarmonyPatch(typeof(JobDriver_Ingest), "ExposeData")]
    static class JobDriver_Ingest_ExposeData_CommonSensePatch
    {
        internal static bool Prepare()
        {
            return !Settings.optimal_patching_in_use || Settings.adv_cleaning_ingest;
        }

        internal static void Postfix(JobDriver_Ingest __instance)
        {
            if (!Settings.adv_cleaning_ingest)
                return;

            JobDriverData.Get(__instance).ExposeData();
        }
    }
}
