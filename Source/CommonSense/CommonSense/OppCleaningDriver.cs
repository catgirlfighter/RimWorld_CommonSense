using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace CommonSense
{
    
    
    static class OppCleaningDriver
    {
        //protected override IEnumerable<Toil> JobDriver_DoBill.MakeNewToils()
        [HarmonyPatch(typeof(JobDriver_DoBill), "MakeNewToils")]
        static class JobDriver_DoBill_MakeNewToils_CommonSensePatch
        {
            public class JobDriver_DoBill_Access: JobDriver_DoBill
            {
                public Map MapCrutch()
                {
                    return Map;
                }
                //cloning private methods :T
                public static Toil JumpToCollectNextIntoHandsForBillCrutch(Toil gotoGetTargetToil, TargetIndex ind)
                {
                    Toil toil = new Toil();
                    toil.initAction = delegate ()
                    {
                        Pawn actor = toil.actor;
                        if (actor.carryTracker.CarriedThing == null)
                        {
                            Log.Error("JumpToAlsoCollectTargetInQueue run on " + actor + " who is not carrying something.", false);
                            return;
                        }
                        if (actor.carryTracker.Full)
                        {
                            return;
                        }
                        Job curJob = actor.jobs.curJob;
                        List<LocalTargetInfo> targetQueue = curJob.GetTargetQueue(ind);
                        if (targetQueue.NullOrEmpty<LocalTargetInfo>())
                        {
                            return;
                        }
                        for (int i = 0; i < targetQueue.Count; i++)
                        {
                            if (GenAI.CanUseItemForWork(actor, targetQueue[i].Thing))
                            {
                                if (targetQueue[i].Thing.CanStackWith(actor.carryTracker.CarriedThing))
                                {
                                    if ((float)(actor.Position - targetQueue[i].Thing.Position).LengthHorizontalSquared <= 64f)
                                    {
                                        int num = (actor.carryTracker.CarriedThing != null) ? actor.carryTracker.CarriedThing.stackCount : 0;
                                        int num2 = curJob.countQueue[i];
                                        num2 = Mathf.Min(num2, targetQueue[i].Thing.def.stackLimit - num);
                                        num2 = Mathf.Min(num2, actor.carryTracker.AvailableStackSpace(targetQueue[i].Thing.def));
                                        if (num2 > 0)
                                        {
                                            curJob.count = num2;
                                            curJob.SetTarget(ind, targetQueue[i].Thing);
                                            List<int> countQueue;
                                            int index;
                                            (countQueue = curJob.countQueue)[index = i] = countQueue[index] - num2;
                                            if (curJob.countQueue[i] <= 0)
                                            {
                                                curJob.countQueue.RemoveAt(i);
                                                targetQueue.RemoveAt(i);
                                            }
                                            actor.jobs.curDriver.JumpToToil(gotoGetTargetToil);
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                    };
                    return toil;
                }
            }

            static IEnumerable<Toil> DoMakeToils(JobDriver_DoBill_Access __instance)
            {
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
                    if (__instance.job.GetTarget(TargetIndex.A).Thing is Filth)
                        return false;
                    
                    IBillGiver billGiver = __instance.job.GetTarget(TargetIndex.A).Thing as IBillGiver;
                    if (billGiver != null)
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
                Toil gotoBillGiver = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
                Toil CheckFilth = new Toil();
                CheckFilth.initAction = delegate ()
                {
                    if (__instance.job.GetTargetQueue(TargetIndex.A).NullOrEmpty<LocalTargetInfo>())
                    {
                        Job curJob = CheckFilth.actor.jobs.curJob;
                        LocalTargetInfo A = __instance.job.GetTarget(TargetIndex.A);
                        IEnumerable<Filth> l = OpportunisticTasks.SelectAllFilth(CheckFilth.actor, A);
                        OpportunisticTasks.AddFilthToQueue(curJob, TargetIndex.A, l, CheckFilth.actor);
                        curJob.targetQueueA.Add(A);
                    }
                };

                yield return new Toil
                {
                    initAction = delegate ()
                    {
                        if (__instance.job.targetQueueB != null && __instance.job.targetQueueB.Count == 1)
                        {
                            UnfinishedThing unfinishedThing = __instance.job.targetQueueB[0].Thing as UnfinishedThing;
                            if (unfinishedThing != null)
                            {
                                unfinishedThing.BoundBill = (Bill_ProductionWithUft)__instance.job.bill;
                            }
                        }
                    }
                };

                yield return Toils_Jump.JumpIf(CheckFilth, () => __instance.job.GetTargetQueue(TargetIndex.B).NullOrEmpty<LocalTargetInfo>());
                Toil extract = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.B, true);
                yield return extract;
                Toil getToHaulTarget = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
                yield return getToHaulTarget;
                yield return Toils_Haul.StartCarryThing(TargetIndex.B, true, false, true);
                yield return JobDriver_DoBill_Access.JumpToCollectNextIntoHandsForBillCrutch(getToHaulTarget, TargetIndex.B);
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell).FailOnDestroyedOrNull(TargetIndex.B);
                Toil findPlaceTarget = Toils_JobTransforms.SetTargetToIngredientPlaceCell(TargetIndex.A, TargetIndex.B, TargetIndex.C);
                yield return findPlaceTarget;
                yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.C, findPlaceTarget, false);
                yield return Toils_Jump.JumpIfHaveTargetInQueue(TargetIndex.B, extract);

                //cleaning part
                yield return CheckFilth;
                yield return Toils_Jump.JumpIf(gotoBillGiver, () => __instance.job.GetTargetQueue(TargetIndex.A).NullOrEmpty<LocalTargetInfo>());
                Toil  CleanFilthList = Toils_JobTransforms.ClearDespawnedNullOrForbiddenQueuedTargets(TargetIndex.A, null);
                yield return CleanFilthList;
                yield return Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A, true);
                yield return Toils_Jump.JumpIf(gotoBillGiver, () => __instance.job.GetTargetQueue(TargetIndex.A).NullOrEmpty<LocalTargetInfo>());
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, CleanFilthList).JumpIfOutsideHomeArea(TargetIndex.A, CleanFilthList);
                Toil clean = new Toil();
                clean.initAction = delegate ()
                {
                    Filth filth = __instance.job.GetTarget(TargetIndex.A).Thing as Filth;
                    __instance.billStartTick = 0;
                    __instance.ticksSpentDoingRecipeWork = 0;
                    __instance.workLeft = (float)filth.def.filth.cleaningWorkToReduceThickness * (float)filth.thickness;
                };
                clean.tickAction = delegate ()
                {
                    Filth filth = __instance.job.GetTarget(TargetIndex.A).Thing as Filth;
                    __instance.billStartTick += 1;
                    __instance.ticksSpentDoingRecipeWork += 1;
                    if (__instance.billStartTick > filth.def.filth.cleaningWorkToReduceThickness)
                    {
                        filth.ThinFilth();
                        __instance.billStartTick = 0;
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
                clean.WithProgressBar(TargetIndex.A, () => (float)__instance.ticksSpentDoingRecipeWork / __instance.workLeft, true, -0.5f);
                clean.PlaySustainerOrSound(() => SoundDefOf.Interact_CleanFilth);
                clean.JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, CheckFilth);
                clean.JumpIfOutsideHomeArea(TargetIndex.A, CheckFilth);
                yield return clean;
                yield return Toils_Jump.Jump(CheckFilth);

                //normal scenario
                yield return gotoBillGiver;
                yield return Toils_Recipe.MakeUnfinishedThingIfNeeded();
                yield return Toils_Recipe.DoRecipeWork().FailOnDespawnedNullOrForbiddenPlacedThings().FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
                yield return Toils_Recipe.FinishRecipeAndStartStoringProduct();
                if (!__instance.job.RecipeDef.products.NullOrEmpty<ThingDefCountClass>() || !__instance.job.RecipeDef.specialProducts.NullOrEmpty<SpecialProductType>())
                {
                    yield return Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null);
                    Toil carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
                    yield return carryToCell;
                    yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carryToCell, true);
                    Toil recount = new Toil();
                    recount.initAction = delegate ()
                    {
                        Bill_Production bill_Production = recount.actor.jobs.curJob.bill as Bill_Production;
                        if (bill_Production != null && bill_Production.repeatMode == BillRepeatModeDefOf.TargetCount)
                        {
                            __instance.MapCrutch().resourceCounter.UpdateResourceCounts();
                        }
                    };
                    yield return recount;
                }
                yield break;
            }

            static bool Prefix(ref IEnumerable<Toil> __result, ref JobDriver_DoBill_Access __instance)
            {
                if (!Settings.adv_cleaning)
                    return true;

                __result = DoMakeToils(__instance);
                return false;
            }
        }
    }
}
