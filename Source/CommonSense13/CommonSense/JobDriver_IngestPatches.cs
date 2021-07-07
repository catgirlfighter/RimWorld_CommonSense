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
                yield return reserveChewSpot(driver.pawn, TargetIndex.A, TargetIndex.B);

                Toil gotospot = gotoSpot(TargetIndex.B).FailOnDestroyedOrNull(TargetIndex.A);

                if (!Utility.IncapableOfCleaning(driver.pawn))
                {

                    TargetIndex filthListIndex = TargetIndex.B;
                    TargetIndex progIndex = TargetIndex.A;
                    Toil FilthList = new Toil();
                    FilthList.initAction = delegate ()
                    {
                        Job curJob = FilthList.actor.jobs.curJob;
                        //
                        if (curJob.GetTargetQueue(filthListIndex).NullOrEmpty())
                        {
                            LocalTargetInfo A = curJob.GetTarget(filthListIndex);
                            if (!A.HasThing) return;
                            IEnumerable<Filth> l = Utility.SelectAllFilth(FilthList.actor, A, Settings.adv_clean_num);
                            Utility.AddFilthToQueue(curJob, filthListIndex, l, FilthList.actor);
                            FilthList.actor.ReserveAsManyAsPossible(curJob.GetTargetQueue(filthListIndex), curJob);
                            curJob.GetTargetQueue(filthListIndex).Add(A);
                        }
                    };

                    yield return FilthList;
                    yield return Toils_Jump.JumpIf(gotospot, () => driver.job.GetTargetQueue(filthListIndex).NullOrEmpty());
                    Toil nextTarget = Toils_JobTransforms.ExtractNextTargetFromQueue(filthListIndex, true);
                    yield return nextTarget;
                    yield return Toils_Jump.JumpIf(gotospot, () => driver.job.GetTargetQueue(filthListIndex).NullOrEmpty());
                    yield return Toils_Goto.GotoThing(filthListIndex, PathEndMode.Touch).JumpIfDespawnedOrNullOrForbidden(filthListIndex, nextTarget).JumpIfOutsideHomeArea(filthListIndex, nextTarget);
                    //
                    if (driver.job.GetTargetQueue(progIndex).Count == 0)
                        driver.job.GetTargetQueue(progIndex).Add(new IntVec3(0, 0, 0));
                    //
                    Toil clean = new Toil();
                    clean.initAction = delegate ()
                    {
                        Filth filth = clean.actor.jobs.curJob.GetTarget(filthListIndex).Thing as Filth;
                        var progQue = clean.actor.jobs.curJob.GetTargetQueue(progIndex);
                        progQue[0] = new IntVec3(0, 0, (int)filth.def.filth.cleaningWorkToReduceThickness * filth.thickness);
                    };
                    clean.tickAction = delegate ()
                    {
                        Filth filth = clean.actor.jobs.curJob.GetTarget(filthListIndex).Thing as Filth;
                        var progQue = clean.actor.jobs.curJob.GetTargetQueue(progIndex);
                        IntVec3 iv = progQue[0].Cell;
                        iv.x += 1;
                        iv.y += 1;
                        if (iv.x > filth.def.filth.cleaningWorkToReduceThickness)
                        {
                            filth.ThinFilth();
                            iv.x = 0;
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
                    clean.WithProgressBar(filthListIndex,
                        delegate ()
                        {
                            var q = driver.job.GetTargetQueue(progIndex)[0];
                            float result = (float)q.Cell.y / q.Cell.z;
                            return result;
                        }
                        , true, -0.5f);
                    clean.PlaySustainerOrSound(() => SoundDefOf.Interact_CleanFilth);
                    clean.JumpIfDespawnedOrNullOrForbidden(filthListIndex, nextTarget);
                    clean.JumpIfOutsideHomeArea(filthListIndex, nextTarget);
                    clean.FailOnDestroyedOrNull(TargetIndex.A);
                    yield return clean;
                    yield return Toils_Jump.Jump(nextTarget);
                }
                yield return gotospot;
            }
            yield return Toils_Ingest.FindAdjacentEatSurface(TargetIndex.B, TargetIndex.A);
            yield break;
        }

        static bool Prefix(ref IEnumerable<Toil> __result, JobDriver_Ingest __instance, Toil chewToil)
        {
            if (!Settings.adv_cleaning_ingest)
                return true;
            //
            __result = prepToils(__instance, chewToil);

            return false;
        }

        public static Toil reserveChewSpot(Pawn pawn, TargetIndex ingestibleInd, TargetIndex StoreToInd)
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
                    if (t.IsForbidden(pawn))
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
                    if (t.HostileTo(pawn))
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
                    thing = GenClosest.ClosestThingReachable(actor.Position, actor.Map, ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), PathEndMode.OnCell, TraverseParms.For(actor, Danger.Deadly, TraverseMode.ByPawn, false), thing2.def.ingestible.chairSearchRadius, (Thing t) => baseChairValidator(t) && t.Position.GetDangerFor(pawn, t.Map) == Danger.None, null, 0, -1, false, RegionType.Set_Passable, false);
                }
                if (thing == null)
                {
                    intVec = RCellFinder.SpotToChewStandingNear(actor, actor.CurJob.GetTarget(ingestibleInd).Thing);
                    Danger chewSpotDanger = intVec.GetDangerFor(pawn, actor.Map);
                    if (chewSpotDanger != Danger.None)
                    {
                        thing = GenClosest.ClosestThingReachable(actor.Position, actor.Map, ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), PathEndMode.OnCell, TraverseParms.For(actor, Danger.Deadly, TraverseMode.ByPawn, false), thing2.def.ingestible.chairSearchRadius, (Thing t) => baseChairValidator(t) && t.Position.GetDangerFor(pawn, t.Map) <= chewSpotDanger, null, 0, -1, false, RegionType.Set_Passable, false);
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
    }
}
