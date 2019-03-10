using System;
using System.Linq;
using System.Collections.Generic;
using Harmony;
using RimWorld;
using Verse;
using Verse.AI;

namespace CommonSense
{
    class CompUnloadChecker : ThingComp
    {
        public bool ShouldUnload = false;
        public bool WasInInventory = false;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ShouldUnload, "CommonSenseShouldUnload", defaultValue: false);
            Scribe_Values.Look(ref ShouldUnload, "CommonSenseWasInBackpack", defaultValue: false);
        }
    }

    [HarmonyPatch(typeof(GenDrop), nameof(GenDrop.TryDropSpawn))]
    static class GenPlace_TryPlaceThing_CommonSensePatch
    {
        
        static void Postfix(Thing thing, IntVec3 dropCell, Map map, ThingPlaceMode mode, Thing resultingThing, Action<Thing, int> placedAction, Predicate<IntVec3> nearPlaceValidator)
        {
            CompUnloadChecker UChecker = resultingThing.TryGetComp<CompUnloadChecker>();
            if (UChecker != null)
            {
                UChecker.WasInInventory = false;
                UChecker.ShouldUnload = false;
            }
            
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), "InitializeComps")]
    static class ThingWithComps_InitializeComps_CommonSensePatch
    {
        static void Postfix(ThingWithComps __instance)
        {
            if (__instance.def.comps.Count > 0 && __instance.def.stackLimit > 0)
            {
                ThingComp thingComp = (ThingComp)Activator.CreateInstance(typeof(CompUnloadChecker));
                thingComp.parent = __instance;
                __instance.AllComps.Add(thingComp);
            }
            //thingComp.Initialize(null);
        }
    }

    [HarmonyPatch(typeof(JobGiver_UnloadYourInventory), "TryGiveJob", new Type[] { typeof(Pawn) })]
    static class JobGiver_UnloadYourInventory_TryGiveJob_CommonSensePatch
    {
        static bool Prefix(ref Job __result, ref JobGiver_UnloadYourInventory __instance, ref Pawn pawn)
        {
            Thing thing = pawn.inventory.innerContainer.FirstOrDefault(x => x.TryGetComp<CompUnloadChecker>() != null && x.TryGetComp<CompUnloadChecker>().ShouldUnload);
            if (thing != null)
            {
                __result = new Job(CommonSenseJobDefOf.UnloadMarkedItems);
                return false;
            }
            return true;
        }
    }

    [DefOf]
    public static class CommonSenseJobDefOf
    {
        public static JobDef UnloadMarkedItems;
    }

    //slightly modified UnloadYourInventory
    public class JobDriver_UnloadMarkedItems : JobDriver
    {
        // Token: 0x06000389 RID: 905 RVA: 0x00023E4C File Offset: 0x0002224C
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref this.countToDrop, "countToDrop", -1, false);
        }

        // Token: 0x0600038A RID: 906 RVA: 0x00023E66 File Offset: 0x00022266
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        // Token: 0x0600038B RID: 907 RVA: 0x00023E6C File Offset: 0x0002226C
        Thing getFirstMarked()
        {
            return pawn.inventory.innerContainer.FirstOrDefault(x => x.TryGetComp<CompUnloadChecker>() != null && x.TryGetComp<CompUnloadChecker>().ShouldUnload);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_General.Wait(10, TargetIndex.None);
            yield return new Toil
            {
                initAction = delegate ()
                {
                    if (getFirstMarked() == null)
                    {
                        base.EndJobWith(JobCondition.Succeeded);
                    }
                    else
                    {
                        Thing MarkedThing = getFirstMarked();
                        ThingCount firstUnloadableThing = MarkedThing == null ? default(ThingCount) : new ThingCount(MarkedThing, MarkedThing.stackCount);
                        //
                        IntVec3 c;
                        if (!StoreUtility.TryFindStoreCellNearColonyDesperate(firstUnloadableThing.Thing, this.pawn, out c))
                        {
                            Thing thing;
                            this.pawn.inventory.innerContainer.TryDrop(firstUnloadableThing.Thing, ThingPlaceMode.Near, firstUnloadableThing.Count, out thing, null, null);
                            base.EndJobWith(JobCondition.Succeeded);
                        }
                        else
                        {
                            this.job.SetTarget(TargetIndex.A, firstUnloadableThing.Thing);
                            this.job.SetTarget(TargetIndex.B, c);
                            this.countToDrop = firstUnloadableThing.Count;
                        }
                    }
                }
            };
            yield return Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.Touch);
            yield return new Toil
            {
                initAction = delegate ()
                {
                    Thing thing = this.job.GetTarget(TargetIndex.A).Thing;
                    if (thing == null || !this.pawn.inventory.innerContainer.Contains(thing))
                    {
                        base.EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    if (!this.pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || !thing.def.EverStorable(false))
                    {
                        this.pawn.inventory.innerContainer.TryDrop(thing, ThingPlaceMode.Near, this.countToDrop, out thing, null, null);
                        base.EndJobWith(JobCondition.Succeeded);
                    }
                    else
                    {
                        this.pawn.inventory.innerContainer.TryTransferToContainer(thing, this.pawn.carryTracker.innerContainer, this.countToDrop, out thing, true);
                        this.job.count = this.countToDrop;
                        this.job.SetTarget(TargetIndex.A, thing);
                    }
                    thing.SetForbidden(false, false);
                }
            };
            Toil carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
            yield return carryToCell;
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carryToCell, true);
            yield break;
        }

        // Token: 0x04000249 RID: 585
        private int countToDrop = -1;

        // Token: 0x0400024A RID: 586
        private const TargetIndex ItemToHaulInd = TargetIndex.A;

        // Token: 0x0400024B RID: 587
        private const TargetIndex StoreCellInd = TargetIndex.B;

        // Token: 0x0400024C RID: 588
        private const int UnloadDuration = 10;
    }
}
