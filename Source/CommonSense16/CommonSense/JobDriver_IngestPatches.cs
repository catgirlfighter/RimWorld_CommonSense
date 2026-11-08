using System.Collections.Generic;
using HarmonyLib;
using System;
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
                Toil gotospot = Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell).FailOnDestroyedOrNull(TargetIndex.A);

                if (!Utility.IncapableOfCleaning(driver.pawn))
                {
                    TargetIndex filthListIndex = TargetIndex.B;
                    Toil FilthList = Utility.ListFilthToil(filthListIndex);
                    yield return FilthList;
                    yield return Toils_Jump.JumpIf(gotospot, () => driver.job.GetTargetQueue(filthListIndex).NullOrEmpty());
                    Toil nextTarget = Toils_JobTransforms.ExtractNextTargetFromQueue(filthListIndex, true);
                    yield return nextTarget;
                    yield return Toils_Jump.JumpIf(gotospot, () => driver.job.GetTargetQueue(filthListIndex).NullOrEmpty());
                    yield return Toils_Goto.GotoThing(filthListIndex, PathEndMode.Touch).JumpIfDespawnedOrNullOrForbidden(filthListIndex, nextTarget).JumpIfOutsideHomeArea(filthListIndex, nextTarget);
                    Toil CleanFilth = Utility.CleanFilthToil(filthListIndex);
                    yield return CleanFilth;
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
                    Log.Warning($"Can't find valid chair or spot for {actor} trying to ingest {thing}");
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
            {
                yield return JobDriver_PrepareToIngestToils_ToolUser_CommonSensePatch.ReserveChewSpot(TargetIndex.A, TargetIndex.B);
                Toil GoToSpot = Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell).FailOnDestroyedOrNull(TargetIndex.A);

                if (!Utility.IncapableOfCleaning(driver.pawn))
                {
                    TargetIndex filthListIndex = TargetIndex.B;
                    Toil FilthList = Utility.ListFilthToil(filthListIndex);
                    yield return FilthList;
                    yield return Toils_Jump.JumpIf(GoToSpot, () => driver.job.GetTargetQueue(filthListIndex).NullOrEmpty());
                    Toil nextTarget = Toils_JobTransforms.ExtractNextTargetFromQueue(filthListIndex, true);
                    yield return nextTarget;
                    yield return Toils_Jump.JumpIf(GoToSpot, () => driver.job.GetTargetQueue(filthListIndex).NullOrEmpty());
                    yield return Toils_Goto.GotoThing(filthListIndex, PathEndMode.Touch).JumpIfDespawnedOrNullOrForbidden(filthListIndex, nextTarget).JumpIfOutsideHomeArea(filthListIndex, nextTarget);
                    Toil CleanFilth = Utility.CleanFilthToil(filthListIndex).JumpIfDespawnedOrNullOrForbidden(filthListIndex, FilthList).JumpIfOutsideHomeArea(filthListIndex, FilthList);
                    yield return CleanFilth;
                    yield return Toils_Jump.Jump(nextTarget);
                }
                yield return GoToSpot;
            }
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
}
