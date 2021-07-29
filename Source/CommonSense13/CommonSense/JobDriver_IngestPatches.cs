using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Reflection;

namespace CommonSense
{
    [HarmonyPatch(typeof(JobDriver_Ingest), "PrepareToIngestToils_ToolUser")]
    static class JobDriver_PrepareToIngestToils_ToolUser_CommonSensePatch
    {
        static FieldInfo LeatingFromInventory = null;
        static MethodInfo LReserveFood = null;
        static MethodInfo LTakeExtraIngestibles = null;

        static void Prepare()
        {
            LeatingFromInventory = AccessTools.Field(typeof(JobDriver_Ingest), "eatingFromInventory");
            LReserveFood = AccessTools.Method(typeof(JobDriver_Ingest), "ReserveFood");
            LTakeExtraIngestibles = AccessTools.Method(typeof(JobDriver_Ingest), "TakeExtraIngestibles");
        }

        public static Toil makeFilthListToil(TargetIndex targetIndex)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Job curJob = toil.actor.jobs.curJob;
                //
                if (curJob.GetTargetQueue(targetIndex).NullOrEmpty())
                {
                    LocalTargetInfo filthListTarget = curJob.GetTarget(targetIndex);
                    if (!filthListTarget.HasThing) return;
                    IEnumerable<Filth> l = Utility.SelectAllFilth(toil.actor, filthListTarget, Settings.adv_clean_num);
                    Utility.AddFilthToQueue(curJob, targetIndex, l, toil.actor);
                    toil.actor.ReserveAsManyAsPossible(curJob.GetTargetQueue(targetIndex), curJob);
                    curJob.GetTargetQueue(targetIndex).Add(filthListTarget);
                }
            };
            return toil;
        }

        public static Toil makeCleanToil(TargetIndex progListIndex, TargetIndex filthListIndex, Toil nextTarget)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Filth filth = toil.actor.jobs.curJob.GetTarget(filthListIndex).Thing as Filth;
                var progQue = toil.actor.jobs.curJob.GetTargetQueue(progListIndex);
                progQue[0] = new IntVec3(0, 0, (int)filth.def.filth.cleaningWorkToReduceThickness * filth.thickness);
            };
            toil.tickAction = delegate ()
            {
                Filth filth = toil.actor.jobs.curJob.GetTarget(filthListIndex).Thing as Filth;
                var progQue = toil.actor.jobs.curJob.GetTargetQueue(progListIndex);
                IntVec3 iv = progQue[0].Cell;
                iv.x += 1;
                iv.y += 1;
                if (iv.x > filth.def.filth.cleaningWorkToReduceThickness)
                {
                    filth.ThinFilth();
                    iv.x = 0;
                    if (filth.Destroyed)
                    {
                        toil.actor.records.Increment(RecordDefOf.MessesCleaned);
                        toil.actor.jobs.curDriver.ReadyForNextToil();
                        return;
                    }
                }
                progQue[0] = iv;
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithEffect(EffecterDefOf.Clean, filthListIndex);
            toil.WithProgressBar(filthListIndex,
                delegate ()
                {
                    var q = toil.actor.CurJob.GetTargetQueue(progListIndex)[0];
                    float result = (float)q.Cell.y / q.Cell.z;
                    return result;
                }
                , true, -0.5f);
            toil.PlaySustainerOrSound(() => SoundDefOf.Interact_CleanFilth);
            toil.JumpIfDespawnedOrNullOrForbidden(filthListIndex, nextTarget);
            toil.JumpIfOutsideHomeArea(filthListIndex, nextTarget);
            toil.FailOnDestroyedOrNull(TargetIndex.A);
            return toil;
        }

        static IEnumerable<Toil> prepToils(JobDriver_Ingest driver, Toil chewToil)
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
                yield return reserveChewSpot(TargetIndex.A, TargetIndex.B);
                Toil gotospot = gotoSpot(TargetIndex.B).FailOnDestroyedOrNull(TargetIndex.A);

                if (!Utility.IncapableOfCleaning(driver.pawn))
                {
                    TargetIndex filthListIndex = TargetIndex.B;
                    TargetIndex progListIndex = TargetIndex.A;
                    Toil FilthList = makeFilthListToil(filthListIndex);
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
                    Toil clean = makeCleanToil(progListIndex, filthListIndex, nextTarget);
                    yield return clean;
                    yield return Toils_Jump.Jump(nextTarget);
                }
                yield return gotospot;
            }
            yield return Toils_Ingest.FindAdjacentEatSurface(TargetIndex.B, TargetIndex.A);
            yield break;
        }

        public static Toil reserveChewSpot(TargetIndex ingestibleInd, TargetIndex StoreToInd)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                IntVec3 intVec = IntVec3.Invalid;
                Thing thing = null;
                Thing thing2 = actor.CurJob.GetTarget(ingestibleInd).Thing;
                Predicate<Thing> baseChairValidator = delegate (Thing t)
                {
                    if (t.def.building == null || !t.def.building.isSittable)
                    {
                        return false;
                    }
                    if (t.IsForbidden(actor))
                    {
                        return false;
                    }
                    if (!actor.CanReserve(t, 1, -1, null, false))
                    {
                        return false;
                    }
                    if (!t.IsSociallyProper(actor))
                    {
                        return false;
                    }
                    if (t.IsBurning())
                    {
                        return false;
                    }
                    if (t.HostileTo(actor))
                    {
                        return false;
                    }
                    bool result = false;
                    for (int i = 0; i < 4; i++)
                    {
                        Building edifice = (t.Position + GenAdj.CardinalDirections[i]).GetEdifice(t.Map);
                        if (edifice != null && edifice.def.surfaceType == SurfaceType.Eat)
                        {
                            result = true;
                            break;
                        }
                    }
                    return result;
                };
                if (thing2.def.ingestible.chairSearchRadius > 0f)
                {
                    thing = GenClosest.ClosestThingReachable(actor.Position, actor.Map, ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), PathEndMode.OnCell, TraverseParms.For(actor, Danger.Deadly, TraverseMode.ByPawn, false), thing2.def.ingestible.chairSearchRadius, (Thing t) => baseChairValidator(t) && t.Position.GetDangerFor(actor, t.Map) == Danger.None, null, 0, -1, false, RegionType.Set_Passable, false);
                }
                if (thing == null)
                {
                    intVec = RCellFinder.SpotToChewStandingNear(actor, actor.CurJob.GetTarget(ingestibleInd).Thing);
                    Danger chewSpotDanger = intVec.GetDangerFor(actor, actor.Map);
                    if (chewSpotDanger != Danger.None)
                    {
                        thing = GenClosest.ClosestThingReachable(actor.Position, actor.Map, ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), PathEndMode.OnCell, TraverseParms.For(actor, Danger.Deadly, TraverseMode.ByPawn, false), thing2.def.ingestible.chairSearchRadius, (Thing t) => baseChairValidator(t) && t.Position.GetDangerFor(actor, t.Map) <= chewSpotDanger, null, 0, -1, false, RegionType.Set_Passable, false);
                    }
                }
                if (thing == null)
                {
                    actor.Map.pawnDestinationReservationManager.Reserve(actor, actor.CurJob, intVec);
                    actor.CurJob.SetTarget(StoreToInd, intVec);
                }
                else
                {
                    intVec = thing.Position;
                    actor.Reserve(thing, actor.CurJob, 1, -1, null, true);
                    actor.Map.pawnDestinationReservationManager.Reserve(actor, actor.CurJob, intVec);
                    actor.CurJob.SetTarget(StoreToInd, thing);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        public static Toil gotoSpot(TargetIndex gotoInd)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                actor.pather.StartPath(actor.CurJob.GetTarget(gotoInd), PathEndMode.OnCell);
            };
            toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            return toil;
        }

        static bool Prefix(ref IEnumerable<Toil> __result, JobDriver_Ingest __instance, Toil chewToil)
        {
            if (!Settings.adv_cleaning_ingest)
                return true;
            //
            __result = prepToils(__instance, chewToil);

            return false;
        }
    }

    [HarmonyPatch(typeof(JobDriver_Ingest), "PrepareToIngestToils_Dispenser")]
    static class JobDriver_PrepareToIngestToils_Dispenser_CommonSensePatch
    {

        static IEnumerable<Toil> prepToils(JobDriver_Ingest driver)
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell).FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Ingest.TakeMealFromDispenser(TargetIndex.A, driver.pawn);
            if (!driver.pawn.Drafted)
                yield return JobDriver_PrepareToIngestToils_ToolUser_CommonSensePatch.reserveChewSpot(TargetIndex.A, TargetIndex.B);
                Toil gotospot = JobDriver_PrepareToIngestToils_ToolUser_CommonSensePatch.gotoSpot(TargetIndex.B).FailOnDestroyedOrNull(TargetIndex.A);

                if (!Utility.IncapableOfCleaning(driver.pawn))
                {
                    TargetIndex filthListIndex = TargetIndex.B;
                    TargetIndex progListIndex = TargetIndex.A;
                    Toil FilthList = JobDriver_PrepareToIngestToils_ToolUser_CommonSensePatch.makeFilthListToil(filthListIndex);
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
                    Toil clean = JobDriver_PrepareToIngestToils_ToolUser_CommonSensePatch.makeCleanToil(progListIndex, filthListIndex, nextTarget);
                    yield return clean;
                    yield return Toils_Jump.Jump(nextTarget);
                }
                yield return gotospot;

            yield return Toils_Ingest.FindAdjacentEatSurface(TargetIndex.B, TargetIndex.A);
        }

        static bool Prefix(ref IEnumerable<Toil> __result, JobDriver_Ingest __instance)
        {
            if (!Settings.adv_cleaning_ingest)
                return true;
            //
            __result = prepToils(__instance);

            return false;
        }
    }
}
