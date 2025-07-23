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
using System.Threading;
using Unity.Jobs;


namespace CommonSense
{
    /*
    [HarmonyPatch(typeof(WorkGiver_DoBill), "JobOnThing")]
    internal static class WorkGiver_DoBill_JobOnThing_CommonSensePatch
    {        internal static bool Prepare()
        {
            return !Settings.optimal_patching_in_use || Settings.adv_cleaning || Settings.adv_haul_all_ings;
        }

        internal static void Postfix(ref Job __result)
        {
            if (__result?.def != JobDefOf.DoBill || !Settings.adv_cleaning && !Settings.adv_haul_all_ings)
                return;

            var job = JobMaker.MakeJob(CommonSenseJobDefOf.DoBillCommonSense, __result.targetA);
            if (__result.targetQueueA != null) job.targetQueueA = new List<LocalTargetInfo> (__result.targetQueueA);
            if (__result.targetQueueB != null) job.targetQueueB = new List<LocalTargetInfo>(__result.targetQueueB);
            if (__result.countQueue != null) job.countQueue = new List<int>(__result.countQueue);
            job.haulMode = __result.haulMode;
            job.bill = __result.bill;
            __result = job;
        }
    }
    */

    //protected override IEnumerable<Toil> JobDriver_DoBill.MakeNewToils()
    [HarmonyPatch(typeof(JobDriver_DoBill), "MakeNewToils")]
    static class JobDriver_DoBill_MakeNewToils_CommonSensePatch
    {
        internal static bool Prepare()
        {
            return !Settings.optimal_patching_in_use || Settings.adv_cleaning || Settings.adv_haul_all_ings;
        }

        private static readonly MethodInfo LJumpIfTargetInsideBillGiver = AccessTools.Method(typeof(JobDriver_DoBill), "JumpIfTargetInsideBillGiver");
        private static readonly MethodInfo LIngredientPlaceCellsInOrder = AccessTools.Method(typeof(Toils_JobTransforms), "IngredientPlaceCellsInOrder");

        private static Toil CheckList()
        {
            Toil toil = ToilMaker.MakeToil("CheckList");
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                List<LocalTargetInfo> targetQueue = curJob.GetTargetQueue(TargetIndex.B);
                if (targetQueue.NullOrEmpty())
                {
                    actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                }
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
            return toil;
        }

        private static Toil PickupToInventory(JobDriver_DoBill __instance, TargetIndex ind)
        {
            Toil toil;
            toil = ToilMaker.MakeToil("PickUpToInventory");
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.handlingFacing = true;
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                Thing thing = curJob.GetTarget(ind).Thing;
                toil.actor.rotationTracker.FaceTarget(thing);
                bool InventorySpawned = thing.ParentHolder == actor.inventory;
                bool checkforcarry = !InventorySpawned && Toils_Haul.ErrorCheckForCarry(actor, thing, true);
                if (InventorySpawned || !checkforcarry)
                {

                    if (thing.stackCount < curJob.count)
                    {
                        actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    //take some to hands and wait to transfer to backpack
                    Thing splitThing;
                    if (InventorySpawned)
                    {
                        splitThing = thing.SplitOff(curJob.count);
                        if (!actor.inventory.GetDirectlyHeldThings().TryAdd(splitThing, false))
                        {
                            actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                            return;
                        }
                    }
                    else
                    {
                        int stackCount = thing.stackCount;
                        int num = Mathf.Min(new int[] { curJob.count, stackCount });
                        num = actor.carryTracker.TryStartCarry(thing, num);

                        if (num == 0) actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                        if (num < stackCount)
                        {
                            num = curJob.count - num;
                            if (num > 0)
                            {
                                curJob.GetTargetQueue(ind).Insert(0, thing);
                                if (curJob.countQueue == null) curJob.countQueue = new List<int>();
                                curJob.countQueue.Insert(0, num);
                            }
                        }
                        splitThing = actor.carryTracker.CarriedThing;
                        actor.records.Increment(RecordDefOf.ThingsHauled);
                    }

                    curJob.SetTarget(ind, splitThing);
                    //add to que to move it to the bottom of haul list later, important for thing material
                    curJob.GetTargetQueue(ind).Add(splitThing);

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

            toil.tickAction = delegate ()
            {
                __instance.ticksSpentDoingRecipeWork += 1;
                if (__instance.ticksSpentDoingRecipeWork < __instance.workLeft)
                    return;

                Pawn actor = toil.actor;
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
                        } //or from ground or from other inventory to backpack
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

            toil.WithProgressBar(TargetIndex.B, () => __instance.ticksSpentDoingRecipeWork / __instance.workLeft);
            return toil;
        }

        private static Toil TakeToHands()
        {
            //taking to hands
            Toil toil = ToilMaker.MakeToil("TakeToHands");
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                if (actor.IsCarrying())
                {
                    return;
                }
                Job curJob = actor.jobs.curJob;
                List<LocalTargetInfo> targetQueue = curJob.GetTargetQueue(TargetIndex.B);
                //if capacity is respected, list can has more items to carry later
                var i = curJob.countQueue.Count;
                //only pick items from invetory, in normal case it's possible that item is already in hands

                var nextThing = targetQueue[i].Thing;

                if (!targetQueue.NullOrEmpty() && nextThing.ParentHolder == actor.inventory)
                {
                    actor.inventory.innerContainer.TryTransferToContainer(nextThing, actor.carryTracker.innerContainer, false);
                    actor.Reserve(targetQueue[i], curJob);
                }

                curJob.SetTarget(TargetIndex.B, targetQueue[i]);
                targetQueue.RemoveAt(i);
            };
            return toil;
        }

        private static Toil ImitateHaulIfNeeded()
        {
            Toil toil = ToilMaker.MakeToil("ImitateHaul");
            toil.initAction = delegate ()
            {
                //if thing was in que, but not held, means it should be put in the end of hauling list
                Pawn actor = toil.actor;
                var curJob = actor.jobs.curJob;
                Thing thing = curJob.GetTarget(TargetIndex.B).Thing;
                if (actor.carryTracker.innerContainer.Count > 0 || curJob.placedThings == null)
                    return;

                ThingCountClass thingCC = curJob.placedThings.FirstOrDefault(x => x.thing == thing);
                if (thingCC != null)
                {
                    curJob.placedThings.Remove(thingCC);
                    curJob.placedThings.Add(thingCC);
                }
            };
            return toil;
        }

        public static Toil SetTargetToIngredientPlaceCell(TargetIndex facilityInd, TargetIndex carryItemInd, TargetIndex cellTargetInd)
        {
            //lets NOT stack items in the same cell
            //becaue it breaks order of hauled items, and order is important to primary resource
            Toil toil = ToilMaker.MakeToil("SetTargetToIngredientPlaceCell");
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                Thing thing = curJob.GetTarget(carryItemInd).Thing;
                IntVec3 c = IntVec3.Invalid;
                foreach (IntVec3 intVec in (IEnumerable<IntVec3>)LIngredientPlaceCellsInOrder.Invoke(null, new object[] { curJob.GetTarget(facilityInd).Thing }))
                {
                    if (GenSpawn.CanSpawnAt(thing.def, intVec, actor.Map, null, true))
                    {
                        if (!c.IsValid)
                        {
                            c = intVec;
                        }
                        bool flag = false;
                        List<Thing> list = actor.Map.thingGrid.ThingsListAt(intVec);
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (list[i].def.category == ThingCategory.Item/* && (!list[i].CanStackWith(thing) || list[i].stackCount == list[i].def.stackLimit)*/)
                            {
                                flag = true;
                                break;
                            }
                        }
                        if (!flag)
                        {
                            curJob.SetTarget(cellTargetInd, intVec);
                            return;
                        }
                    }
                }
                curJob.SetTarget(cellTargetInd, c);
            };
            return toil;
        }
        private static Toil CleanFilth(JobDriver_DoBill __instance)
        {
            Toil toil = ToilMaker.MakeToil("CleanBillPlace");
            toil.initAction = delegate ()
            {
                Filth filth = toil.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing as Filth;
                __instance.billStartTick = 0;
                __instance.ticksSpentDoingRecipeWork = 0;
                __instance.workLeft = filth.def.filth.cleaningWorkToReduceThickness * filth.thickness * 100;
            };
            toil.tickAction = delegate ()
            {
                Filth filth = toil.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing as Filth;
                float statValueAbstract = filth.Position.GetTerrain(filth.Map).GetStatValueAbstract(StatDefOf.CleaningTimeFactor, null);
                float num = toil.actor.GetStatValue(StatDefOf.CleaningSpeed, true, -1);
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
                        toil.actor.records.Increment(RecordDefOf.MessesCleaned);
                        __instance.ReadyForNextToil();
                        return;
                    }
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithEffect(EffecterDefOf.Clean, TargetIndex.A);
            toil.WithProgressBar(TargetIndex.A, () => __instance.ticksSpentDoingRecipeWork / __instance.workLeft, true, -0.5f);
            toil.PlaySustainerOrSound(() => SoundDefOf.Interact_CleanFilth);
            return toil;
        }

        private static IEnumerable<Toil> DoMakeToils(JobDriver_DoBill __instance)
        {
            //
            //normal scenario
            //
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
            //
            //hauling patch
            //
            if (Settings.adv_haul_all_ings && __instance.pawn.Faction == Faction.OfPlayer && __instance.pawn.RaceProps.Humanlike)
            {
                Toil TakeToHands = JobDriver_DoBill_MakeNewToils_CommonSensePatch.TakeToHands();
                Toil getToHaulTarget = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch, true).FailOnForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
                Toil extract = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.B, true);
                Toil keepTakingToInventory = Toils_Jump.JumpIf(extract, () => !__instance.job.countQueue.NullOrEmpty() && !__instance.pawn.IsCarrying());
                Toil keepTakingToHands = Toils_Jump.JumpIf(TakeToHands, () => __instance.job.countQueue.Count < __instance.job.targetQueueB.Count);
                Toil JumpToKeepTakingToInventory = Toils_Jump.JumpIfHaveTargetInQueue(TargetIndex.B, keepTakingToInventory);
                Toil PickupToInventory = JobDriver_DoBill_MakeNewToils_CommonSensePatch.PickupToInventory(__instance, TargetIndex.B);
                yield return CheckList();
                yield return extract;
                yield return (Toil)LJumpIfTargetInsideBillGiver.Invoke(__instance, new object[] { keepTakingToInventory, TargetIndex.B, TargetIndex.A });
                yield return Toils_Jump.JumpIf(PickupToInventory, () => __instance.job.targetB.Thing.ParentHolder == __instance.pawn.inventory);
                yield return getToHaulTarget;
                yield return PickupToInventory;
                yield return keepTakingToInventory;
                yield return Toils_Jump.JumpIf(JumpToKeepTakingToInventory, () => __instance.job.targetQueueB.Count <= __instance.job.countQueue.Count);
                yield return TakeToHands;
                yield return ImitateHaulIfNeeded();
                yield return Toils_Jump.JumpIf(keepTakingToHands, () => !__instance.pawn.IsCarrying());
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell).FailOnDestroyedOrNull(TargetIndex.B);
                if (placeInBillGiver)
                {
                    yield return Toils_Haul.DepositHauledThingInContainer(TargetIndex.A, TargetIndex.B, null);
                }
                else
                {
                    Toil findPlaceTarget = SetTargetToIngredientPlaceCell(TargetIndex.A, TargetIndex.B, TargetIndex.C);
                    yield return findPlaceTarget;
                    yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.C, findPlaceTarget, false, false);
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
                }
            }
            //
            //cleaning patch
            //
            if (Settings.adv_cleaning && !Utility.IncapableOfCleaning(__instance.pawn))
            {
                yield return Utility.ListFilth(TargetIndex.A);
                yield return Toils_Jump.JumpIf(gotoBillGiver, () => __instance.job.GetTargetQueue(TargetIndex.A).NullOrEmpty());
                Toil CleanFilthList = Toils_JobTransforms.ClearDespawnedNullOrForbiddenQueuedTargets(TargetIndex.A, null);
                yield return CleanFilthList;
                yield return Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A, true);
                yield return Toils_Jump.JumpIf(gotoBillGiver, () => __instance.job.GetTargetQueue(TargetIndex.A).NullOrEmpty());
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, CleanFilthList).JumpIfOutsideHomeArea(TargetIndex.A, CleanFilthList);

                Toil CleanFilth = JobDriver_DoBill_MakeNewToils_CommonSensePatch.CleanFilth(__instance);
                CleanFilth.JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, CleanFilthList);
                CleanFilth.JumpIfOutsideHomeArea(TargetIndex.A, CleanFilthList);
                yield return CleanFilth;
                yield return Toils_Jump.Jump(CleanFilthList);
            }
            //
            //continuation of normal scenario
            //
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