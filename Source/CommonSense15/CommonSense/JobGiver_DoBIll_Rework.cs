using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using UnityEngine;


namespace CommonSense
{
    //protected override IEnumerable<Toil> JobDriver_DoBill.MakeNewToils()
    [HarmonyPatch(typeof(JobDriver_DoBill), "MakeNewToils")]
    static class JobDriver_DoBill_MakeNewToils_CommonSensePatch
    {
        internal static bool Prepare()
        {
            return !Settings.optimal_patching_in_use || Settings.adv_cleaning || Settings.adv_haul_all_ings;
        }

        private static readonly MethodInfo LJumpIfTargetInsideBillGiver = AccessTools.Method(typeof(JobDriver_DoBill), "JumpIfTargetInsideBillGiver");

        private static IEnumerable<Toil> DoMakeToils(JobDriver_DoBill __instance)
        {
            //normal scenario
            __instance.AddEndCondition(delegate
            {
                Thing thing = __instance.GetActor().jobs.curJob.GetTarget(TargetIndex.A).Thing;
                if (thing is Building && !thing.Spawned)
                {
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing;
            });
            __instance.FailOnBurningImmobile(TargetIndex.A);
            __instance.FailOn(delegate ()
            {
                if (__instance.job.GetTarget(TargetIndex.A).Thing is Filth) //mod
                    return false;

                if (__instance.job.GetTarget(TargetIndex.A).Thing is IBillGiver billGiver)
                {
                    if (__instance.job.bill.DeletedOrDereferenced)
                    {
                        return true;
                    }
                    if (!billGiver.CurrentlyUsableForBills())
                    {
                        return true;
                    }
                }
                return false;
            });

            bool placeInBillGiver = __instance.BillGiver is Building_MechGestator;
            Toil gotoBillGiver = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil toil = ToilMaker.MakeToil("MakeNewToils");
            toil.initAction = delegate ()
            {
                if (__instance.job.targetQueueB != null && __instance.job.targetQueueB.Count == 1)
                {
                    if (__instance.job.targetQueueB[0].Thing is UnfinishedThing unfinishedThing)
                    {
                        unfinishedThing.BoundBill = (Bill_ProductionWithUft)__instance.job.bill;
                    }
                }
                __instance.job.bill.Notify_DoBillStarted(__instance.pawn);
            };
            yield return toil;
            yield return Toils_Jump.JumpIf(gotoBillGiver, () => __instance.job.GetTargetQueue(TargetIndex.B).NullOrEmpty<LocalTargetInfo>());

            // "started 10 jobs in one tick" fix from SmartMedicine: "Drop the [thing] so that you can then pick it up. Ya really."
            // https://github.com/alextd/RimWorld-SmartMedicine/blob/84e7ac3e84a7f68dd7c7ed493296c0f9d7103f8e/Source/InventorySurgery.cs#L72
            Toil DropTargetThingIfInInventory = ToilMaker.MakeToil("DropTargetThingIfInInventory");
            DropTargetThingIfInInventory.initAction = delegate
            {
                Pawn actor = DropTargetThingIfInInventory.actor;
                Job curJob = actor.jobs.curJob;
                Thing thing = curJob.GetTarget(TargetIndex.B).Thing;

                if (thing.holdingOwner != null)
                {
                    int count = Mathf.Min(curJob.count, actor.carryTracker.AvailableStackSpace(thing.def), thing.stackCount);
                    if (count < 1) return;

                    var owner = thing.holdingOwner;
                    Map rootMap = ThingOwnerUtility.GetRootMap(owner.Owner);
                    IntVec3 rootPosition = ThingOwnerUtility.GetRootPosition(owner.Owner);
                    if (rootMap == null || !rootPosition.IsValid)
                        return;
                    if (owner.TryDrop(thing, ThingPlaceMode.Near, count, out var droppedThing))
                    {
                        curJob.SetTarget(TargetIndex.B, droppedThing);
                    }
                }
            };
            DropTargetThingIfInInventory.defaultCompleteMode = ToilCompleteMode.Instant;

            //hauling patch
            if (Settings.adv_haul_all_ings && __instance.pawn.Faction == Faction.OfPlayer && __instance.pawn.RaceProps.Humanlike)
            {
                Toil checklist = ToilMaker.MakeToil("checklist");
                checklist.initAction = delegate ()
                {
                    Pawn actor = checklist.actor;
                    Job curJob = actor.jobs.curJob;
                    List<LocalTargetInfo> targetQueue = curJob.GetTargetQueue(TargetIndex.B);
                    if (targetQueue.NullOrEmpty())
                        actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                    else
                        foreach (var target in (targetQueue))
                        {
                            if (target == null || target.Thing.DestroyedOrNull())
                            {
                                actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                                break;
                            }
                        }
                };

                Toil PickUpToInventory;
                List<LocalTargetInfo> L = __instance.job.GetTargetQueue(TargetIndex.B);
                //if (L.Count == 0 && __instance.job.targetB.Thing?.ParentHolder == null)
                //{
                //    PickUpToInventory = Toils_Haul.StartCarryThing(TargetIndex.B, true, false, true, false);
                //}
                //else
                //{
                PickUpToInventory = ToilMaker.MakeToil("PickUpToInventory");
                PickUpToInventory.defaultCompleteMode = ToilCompleteMode.Never;
                PickUpToInventory.handlingFacing = true;
                PickUpToInventory.initAction = delegate ()
                {
                    Pawn actor = PickUpToInventory.actor;
                    Job curJob = actor.jobs.curJob;
                    Thing thing = curJob.GetTarget(TargetIndex.B).Thing;
                    PickUpToInventory.actor.rotationTracker.FaceTarget(thing);
                    bool InventorySpawned = thing.ParentHolder == actor.inventory;
                    bool checkforcarry = !InventorySpawned && Toils_Haul.ErrorCheckForCarry(actor, thing);
                    if (InventorySpawned || !checkforcarry)
                    {
                        if (thing.stackCount < curJob.count)
                        {
                            actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                            return;
                        }

                        //take some to hands and wait to transfer to backpack
                        Thing splitThing;
                        if (curJob.count < 0)
                        {
                            splitThing = thing;
                        }
                        else
                        {
                            splitThing = thing.SplitOff(curJob.count);
                        }

                        if (splitThing.ParentHolder != actor.inventory)
                        {
                            if (InventorySpawned)
                            {
                                if (!actor.inventory.GetDirectlyHeldThings().TryAdd(splitThing, false))
                                {
                                    actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                                    return;
                                }
                            }
                            else if (!actor.carryTracker.GetDirectlyHeldThings().TryAdd(splitThing, false))
                            {
                                actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                                return;
                            }
                        }

                        if (!actor.Map.reservationManager.ReservedBy(splitThing, actor))
                            actor.Reserve(splitThing, curJob);
                        curJob.SetTarget(TargetIndex.B, splitThing);
                        //add to que to move it to the bottom of haul list later, important for thing material
                        curJob.GetTargetQueue(TargetIndex.B).Add(splitThing);

                        if (splitThing != thing && actor.Map.reservationManager.ReservedBy(thing, actor, curJob))
                        {
                            actor.Map.reservationManager.Release(thing, actor, curJob);
                        }

                        if (InventorySpawned
                        || curJob.countQueue.Count < 1
                        || Settings.adv_respect_capacity
                        && (MassUtility.GearAndInventoryMass(actor) + splitThing.stackCount * splitThing.GetStatValue(StatDefOf.Mass)) / MassUtility.Capacity(actor) > 1f)
                        {
                            __instance.ReadyForNextToil();
                            return;
                        }
                    }

                    __instance.billStartTick = 0;
                    __instance.ticksSpentDoingRecipeWork = 0;
                    __instance.workLeft = Math.Max(30, actor.jobs.curJob.count * thing.def.BaseMass / 10);
                };

                PickUpToInventory.tickAction = delegate ()
                {
                    __instance.ticksSpentDoingRecipeWork += 1;

                    if (__instance.ticksSpentDoingRecipeWork < __instance.workLeft)
                        return;

                    Pawn actor = PickUpToInventory.actor;
                    Job curJob = actor.jobs.curJob;
                    Thing thing = curJob.GetTarget(TargetIndex.B).Thing;

                    List<LocalTargetInfo> targetQueue = curJob.GetTargetQueue(TargetIndex.B);
                    bool InventorySpawned = thing.ParentHolder == actor.inventory;
                    bool CarrySpawned = thing.ParentHolder == actor.carryTracker;

                    if (InventorySpawned || CarrySpawned || !Toils_Haul.ErrorCheckForCarry(actor, thing))
                    {
                        //try to transfer
                        if (thing.ParentHolder != actor.inventory)
                        {
                            //from hands to backpack
                            if (thing.ParentHolder == actor.carryTracker)
                            {
                                if (!actor.carryTracker.innerContainer.TryTransferToContainer(thing, actor.inventory.innerContainer, false))
                                {
                                    actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                                    return;
                                }
                            } //or from ground to backpack
                            else if (thing.ParentHolder != actor.carryTracker)
                                if (thing.ParentHolder != actor.carryTracker & !actor.inventory.GetDirectlyHeldThings().TryAdd(thing, false))
                                {
                                    actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                                    return;
                                }
                        }

                        if (!thing.Destroyed && thing.stackCount != 0)
                        {
                            if (!InventorySpawned)
                            {
                                CompUnloadChecker CUC = thing.TryGetComp<CompUnloadChecker>();
                                if (CUC != null) CUC.ShouldUnload = true;
                            }
                        }
                    }

                    __instance.ReadyForNextToil();
                };

                PickUpToInventory.WithProgressBar(TargetIndex.B, () => __instance.ticksSpentDoingRecipeWork / __instance.workLeft);
                //};

                //taking to hands
                Toil TakeToHands = ToilMaker.MakeToil("TakeToHands");
                TakeToHands.initAction = delegate ()
                {
                    Pawn actor = TakeToHands.actor;
                    if (actor.IsCarrying())
                    {

                        return;
                    }
                    Job curJob = actor.jobs.curJob;
                    List<LocalTargetInfo> targetQueue = curJob.GetTargetQueue(TargetIndex.B);
                    //if capacity is respected, list can has more items to carry later
                    var i = curJob.countQueue.Count;
                    //only pick items from invetory, in normal case it's possible that item is already in hands
                    if (!targetQueue.NullOrEmpty() && targetQueue[i].Thing.ParentHolder == actor.inventory)
                    {
                        actor.inventory.innerContainer.TryTransferToContainer(targetQueue[i].Thing, actor.carryTracker.innerContainer, false);
                        actor.Reserve(targetQueue[i], curJob);
                    }

                    curJob.SetTarget(TargetIndex.B, targetQueue[i]);

                    targetQueue.RemoveAt(i);

                };

                Toil ImitateHaulIfNeeded = ToilMaker.MakeToil("ImitateHaul");
                ImitateHaulIfNeeded.initAction = delegate ()
                {
                    //if thing was in que, but not held, means it should be put in the end of hauling list
                    Pawn actor = TakeToHands.actor;
                    var curJob = actor.jobs.curJob;
                    Thing thing = curJob.GetTarget(TargetIndex.B).Thing;
                    if (actor.carryTracker.innerContainer.Count > 0)
                        return;

                    ThingCountClass thingCC = curJob.placedThings.FirstOrDefault(x => x.thing == thing);
                    if (thingCC != null)
                    {
                        curJob.placedThings.Remove(thingCC);
                        curJob.placedThings.Add(thingCC);
                    }
                };

                Toil getToHaulTarget = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
                Toil extract = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.B, true);
                Toil keepTakingToInventory = Toils_Jump.JumpIf(extract, () => !__instance.job.countQueue.NullOrEmpty() && !__instance.pawn.IsCarrying());
                Toil keepTakingToHands = Toils_Jump.JumpIf(TakeToHands, () => __instance.job.countQueue.Count < __instance.job.targetQueueB.Count);
                Toil JumpToKeepTakingToInventory = Toils_Jump.JumpIfHaveTargetInQueue(TargetIndex.B, keepTakingToInventory);
                yield return checklist;
                yield return extract;
                yield return (Toil)LJumpIfTargetInsideBillGiver.Invoke(__instance, new object[] { keepTakingToInventory, TargetIndex.B, TargetIndex.A });
                yield return Toils_Jump.JumpIf(PickUpToInventory, () => __instance.job.targetB.Thing.ParentHolder == __instance.pawn.inventory);
                yield return getToHaulTarget;
                yield return PickUpToInventory;
                yield return keepTakingToInventory;
                yield return Toils_Jump.JumpIf(JumpToKeepTakingToInventory, () => __instance.job.targetQueueB.Count <= __instance.job.countQueue.Count);
                yield return TakeToHands;
                yield return ImitateHaulIfNeeded;
                yield return Toils_Jump.JumpIf(keepTakingToHands, () => !__instance.pawn.IsCarrying());
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell).FailOnDestroyedOrNull(TargetIndex.B);
                if (placeInBillGiver)
                {
                    yield return Toils_Haul.DepositHauledThingInContainer(TargetIndex.A, TargetIndex.B, null);
                }
                else
                {
                    Toil findPlaceTarget = Toils_JobTransforms.SetTargetToIngredientPlaceCell(TargetIndex.A, TargetIndex.B, TargetIndex.C);
                    yield return findPlaceTarget;
                    yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.C, findPlaceTarget, false);
                    Toil physReserveToil = ToilMaker.MakeToil("CollectIngredientsToils");
                    physReserveToil.initAction = delegate ()
                    {
                        physReserveToil.actor.Map.physicalInteractionReservationManager.Reserve(physReserveToil.actor, physReserveToil.actor.CurJob, physReserveToil.actor.CurJob.GetTarget(TargetIndex.B));
                    };
                    yield return physReserveToil;
                }
                yield return keepTakingToHands;
                yield return JumpToKeepTakingToInventory;
            }
            else
            {
                foreach (Toil toil2 in JobDriver_DoBill.CollectIngredientsToils(TargetIndex.B, TargetIndex.A, TargetIndex.C, false, true, __instance.BillGiver is Building_WorkTableAutonomous))
                {
                    yield return toil2;
                    if (toil2.debugName == "JumpIfTargetInsideBillGiver")
                        yield return DropTargetThingIfInInventory;
                }
            }

            //cleaning patch
            if (Settings.adv_cleaning && !Utility.IncapableOfCleaning(__instance.pawn))
            {
                Toil FilthList = new Toil();
                FilthList.initAction = delegate ()
                {
                    Job curJob = FilthList.actor.jobs.curJob;
                    if (curJob.GetTargetQueue(TargetIndex.A).NullOrEmpty())
                    {
                        LocalTargetInfo A = curJob.GetTarget(TargetIndex.A);

                        if (!Settings.clean_gizmo || A.Thing?.TryGetComp<DoCleanComp>()?.Active != false)
                        {
                            IEnumerable<Filth> l = Utility.SelectAllFilth(FilthList.actor, A, Settings.adv_clean_num);
                            Utility.AddFilthToQueue(curJob, TargetIndex.A, l, FilthList.actor);
                            FilthList.actor.ReserveAsManyAsPossible(curJob.GetTargetQueue(TargetIndex.A), curJob);
                        }
                        curJob.targetQueueA.Add(A);
                    }
                };
                yield return FilthList;
                yield return Toils_Jump.JumpIf(gotoBillGiver, () => __instance.job.GetTargetQueue(TargetIndex.A).NullOrEmpty());
                Toil CleanFilthList = Toils_JobTransforms.ClearDespawnedNullOrForbiddenQueuedTargets(TargetIndex.A, null);
                yield return CleanFilthList;
                yield return Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A, true);
                yield return Toils_Jump.JumpIf(gotoBillGiver, () => __instance.job.GetTargetQueue(TargetIndex.A).NullOrEmpty());
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, CleanFilthList).JumpIfOutsideHomeArea(TargetIndex.A, CleanFilthList);
                Toil clean = ToilMaker.MakeToil("CleanBillPlace");
                clean.initAction = delegate ()
                {
                    Filth filth = clean.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing as Filth;
                    __instance.billStartTick = 0;
                    __instance.ticksSpentDoingRecipeWork = 0;
                    __instance.workLeft = filth.def.filth.cleaningWorkToReduceThickness * filth.thickness * 100;
                };
                clean.tickAction = delegate ()
                {
                    Filth filth = clean.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing as Filth;
                    float statValueAbstract = filth.Position.GetTerrain(filth.Map).GetStatValueAbstract(StatDefOf.CleaningTimeFactor, null);
                    float num = clean.actor.GetStatValue(StatDefOf.CleaningSpeed, true, -1);
                    if (statValueAbstract != 0f)
                    {
                        num /= statValueAbstract;
                    }
                    __instance.billStartTick += Mathf.Max(1, Mathf.RoundToInt(num * 100));
                    __instance.ticksSpentDoingRecipeWork += Mathf.Max(1, Mathf.RoundToInt(num * 100));
                    if (__instance.billStartTick > filth.def.filth.cleaningWorkToReduceThickness * 100)
                    {
                        filth.ThinFilth();
                        __instance.billStartTick -= (int)(filth.def.filth.cleaningWorkToReduceThickness * 100);
                        if (filth.Destroyed)
                        {
                            clean.actor.records.Increment(RecordDefOf.MessesCleaned);
                            __instance.ReadyForNextToil();
                            return;
                        }
                    }
                };
                clean.defaultCompleteMode = ToilCompleteMode.Never;
                clean.WithEffect(EffecterDefOf.Clean, TargetIndex.A);
                clean.WithProgressBar(TargetIndex.A, () => __instance.ticksSpentDoingRecipeWork / __instance.workLeft, true, -0.5f);
                clean.PlaySustainerOrSound(() => SoundDefOf.Interact_CleanFilth);
                clean.JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, CleanFilthList);
                clean.JumpIfOutsideHomeArea(TargetIndex.A, CleanFilthList);
                yield return clean;
                yield return Toils_Jump.Jump(CleanFilthList);
            }

            //continuation of normal scenario
            yield return gotoBillGiver;
            yield return Toils_Recipe.MakeUnfinishedThingIfNeeded();
            yield return Toils_Recipe.DoRecipeWork().FailOnDespawnedNullOrForbiddenPlacedThings(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            yield return Toils_Recipe.CheckIfRecipeCanFinishNow();
            yield return Toils_Recipe.FinishRecipeAndStartStoringProduct(TargetIndex.None);
            //
            yield break;
        }

        internal static bool Prefix(ref IEnumerable<Toil> __result, ref JobDriver_DoBill __instance)
        {
            if (!Settings.adv_cleaning && !Settings.adv_haul_all_ings)
                return true;

            __result = DoMakeToils(__instance);
            return false;
        }
    }
}