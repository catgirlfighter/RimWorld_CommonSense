using System;
using System.Linq;
using System.Collections.Generic;
using Harmony;
using RimWorld;
using Verse;
using Verse.AI;
using System.Collections;

namespace CommonSense
{
    public class CompUnloadChecker : ThingComp
    {
        public bool ShouldUnload = false;
        public bool WasInInventory = false;

        public override void PostExposeData()
        {
            base.PostExposeData();
            if (parent.ParentHolder != null)
            {
                Scribe_Values.Look(ref ShouldUnload, "CommonSenseShouldUnload", defaultValue: false);
                Scribe_Values.Look(ref WasInInventory, "CommonSenseWasInInventory", defaultValue: false);
            }
        }
        public void Init(bool AShouldUnload, bool AWasInInventory)
        {
            ShouldUnload = AShouldUnload;
            WasInInventory = AWasInInventory;
        }

        static public CompUnloadChecker GetChecker(Thing thing, bool InitShouldUnload = false, bool InitWasInInventory = false)
        {
            
            if (!(thing is ThingWithComps) && !typeof(ThingWithComps).IsSubclassOf(thing.GetType()))
                return null;
            ThingWithComps TWC = (ThingWithComps)thing;
            if (TWC.AllComps == null)
                return null;
            CompUnloadChecker thingComp = thing.TryGetComp<CompUnloadChecker>();
            if (thingComp == null)
            {
                thingComp = (CompUnloadChecker)Activator.CreateInstance(typeof(CompUnloadChecker));
                thingComp.parent = TWC;
                TWC.AllComps.Add(thingComp);
            }
            thingComp.ShouldUnload = thingComp.ShouldUnload || InitShouldUnload;
            thingComp.WasInInventory = thingComp.WasInInventory || InitWasInInventory;
            return thingComp;
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

    [HarmonyPatch(typeof(ThingWithComps), "ExposeData")]
    static class ThingWithComps_ExposeData_CommonSensePatch
    {
        static void Postfix(ThingWithComps __instance)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                bool a = false;
                bool b = false;
                Scribe_Values.Look(ref a, "CommonSenseShouldUnload", defaultValue: false);
                Scribe_Values.Look(ref b, "CommonSenseWasInInventory", defaultValue: false);
                if (a || b)
                    CompUnloadChecker.GetChecker(__instance, a, b);
            }
        }
    }

    [HarmonyPatch(typeof(ThingOwner<Thing>), "TryAdd", new Type[] { typeof(Thing), typeof(bool) })]
    static class ThingOwnerThing_TryAdd_CommonSensePatch
    {
        static void Postfix(ThingOwner<Thing> __instance, bool __result, Thing item)
        {
            if (!__result || item.Destroyed || item.stackCount == 0)
                return;

            if (__instance.Owner is Pawn_InventoryTracker)
            {
                CompUnloadChecker.GetChecker(__instance[__instance.Count - 1], false, true);
            }
        }
    }

    [HarmonyPatch(typeof(ThingOwner), "ExposeData")]
    static class ThingOwnerThing_ExposeData_CommonSensePatch
    {
        static void Postfix(ThingOwner __instance)
        {
            if (__instance.Owner is Pawn_InventoryTracker && Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                for (int i = 0; i < __instance.Count; i++)
                    if (__instance[i] != null)
                        CompUnloadChecker.GetChecker(__instance[i], false, true);
            }
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
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref this.countToDrop, "countToDrop", -1, false);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

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
                            pawn.inventory.innerContainer.TryDrop(firstUnloadableThing.Thing, ThingPlaceMode.Near, firstUnloadableThing.Count, out thing, null, null);
                            EndJobWith(JobCondition.Succeeded);
                        }
                        else
                        {
                            job.SetTarget(TargetIndex.A, firstUnloadableThing.Thing);
                            job.SetTarget(TargetIndex.B, c);
                            countToDrop = firstUnloadableThing.Count;
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

        private int countToDrop = -1;
        private const TargetIndex ItemToHaulInd = TargetIndex.A;
        private const TargetIndex StoreCellInd = TargetIndex.B;
        private const int UnloadDuration = 10;
    }
}
