using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Reflection;

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

        public static CompUnloadChecker GetChecker(Thing thing, bool InitShouldUnload = false, bool InitWasInInventory = false)
        {

            if (!(thing is ThingWithComps) && !thing.GetType().IsSubclassOf(typeof(ThingWithComps)))
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

        public static Thing GetFirstMarked(Pawn pawn)
        {
            Thing t = null;
            if (pawn.inventory != null) t = pawn.inventory.innerContainer.FirstOrDefault(x => x.TryGetComp<CompUnloadChecker>()?.ShouldUnload == true);
            if (!Settings.gui_manual_unload) return t;
            if (t == null && pawn.equipment != null) t = pawn.equipment.AllEquipmentListForReading.FirstOrDefault(x => x.TryGetComp<CompUnloadChecker>()?.ShouldUnload == true);
            if (t == null && pawn.apparel != null) t = pawn.apparel.WornApparel.FirstOrDefault(x => x.TryGetComp<CompUnloadChecker>()?.ShouldUnload == true);
            return t;
        }
    }

    [HarmonyPatch(typeof(GenDrop), nameof(GenDrop.TryDropSpawn))]
    public static class GenPlace_TryDropSpawn_NewTmp_CommonSensePatch
    {

        public static void Postfix(Thing thing, IntVec3 dropCell, Map map, ThingPlaceMode mode, Thing resultingThing, Action<Thing, int> placedAction, Predicate<IntVec3> nearPlaceValidator)
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
    public static class ThingWithComps_ExposeData_CommonSensePatch
    {
        public static void Postfix(ThingWithComps __instance)
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
    public static class ThingOwnerThing_TryAdd_CommonSensePatch
    {
        public static void Postfix(ThingOwner<Thing> __instance, bool __result, Thing item)
        {
            if (!__result || item.Destroyed || item.stackCount == 0)
                return;

            if (__instance.Owner is Pawn_InventoryTracker)
            {
                CompUnloadChecker.GetChecker(__instance[__instance.Count - 1], false, true);
            }
        }
    }

    [HarmonyPatch(typeof(JobGiver_UnloadYourInventory), "TryGiveJob", new Type[] { typeof(Pawn) })]
    public static class JobGiver_UnloadYourInventory_TryGiveJob_CommonSensePatch
    {
        public static bool Prefix(ref Job __result, ref JobGiver_UnloadYourInventory __instance, ref Pawn pawn)
        {
            Thing thing = CompUnloadChecker.GetFirstMarked(pawn);
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

        Apparel Apparel = null;
        ThingWithComps Equipment = null;
        float ticker = 0;
        float duration = 0;


        private static bool stillUnloadable(Thing thing)
        {
            CompUnloadChecker c = thing.TryGetComp<CompUnloadChecker>();
            return c != null && c.ShouldUnload;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_General.Wait(10, TargetIndex.None);
            yield return new Toil
            {
                initAction = delegate ()
                {
                    Thing MarkedThing = CompUnloadChecker.GetFirstMarked(pawn);
                    if (MarkedThing == null)
                    {
                        EndJobWith(JobCondition.Succeeded);
                        return;
                    }
                    //
                    if (pawn.equipment != null && pawn.equipment.Contains(MarkedThing))
                    {
                        Equipment = (ThingWithComps)MarkedThing;
                        Apparel = null;
                    }
                    else if (pawn.apparel != null && pawn.apparel.Contains(MarkedThing))
                    {
                        Apparel = (Apparel)MarkedThing;
                        Equipment = null;
                    }
                    else
                    {
                        Equipment = null;
                        Apparel = null;
                    }

                    ThingCount firstUnloadableThing = MarkedThing == null ? default(ThingCount) : new ThingCount(MarkedThing, MarkedThing.stackCount);
                    //IntVec3 c;
                    if (!StoreUtility.TryFindStoreCellNearColonyDesperate(firstUnloadableThing.Thing, pawn, out var c))
                    {
                        pawn.inventory.innerContainer.TryDrop(firstUnloadableThing.Thing, ThingPlaceMode.Near, firstUnloadableThing.Count, out Thing thing, null, null);
                        EndJobWith(JobCondition.Succeeded);
                        return;
                    }

                    job.SetTarget(TargetIndex.A, firstUnloadableThing.Thing);
                    job.SetTarget(TargetIndex.B, c);
                    countToDrop = firstUnloadableThing.Count;
                }
            };
            yield return Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.Touch).FailOnDestroyedOrNull(TargetIndex.A).FailOn(delegate () { return !stillUnloadable(pawn.CurJob.GetTarget(TargetIndex.A).Thing); });

            //preintiating unequip-delay
            Toil unequip = new Toil
            {
                initAction = delegate ()
                {
                    if (Equipment != null)
                        pawn.equipment.TryTransferEquipmentToContainer(Equipment, pawn.inventory.innerContainer);
                    else if (Apparel != null)
                    {
                        ThingOwner<Apparel> a = Traverse.Create(pawn.apparel).Field("wornApparel").GetValue<ThingOwner<Apparel>>();
                        a.TryTransferToContainer(Apparel, pawn.inventory.innerContainer);
                    }
                }
            };
            //if equiped, wait unequipping time
            Toil wait = new Toil
            {
                initAction = delegate ()
                {
                    ticker = 0;
                    duration = Apparel != null ? Apparel.GetStatValue(StatDefOf.EquipDelay, true) * 60f : Equipment != null ? 30 : 0;
                    pawn.pather.StopDead();
                },
                tickAction = delegate ()
                {
                    if (ticker >= duration) ReadyForNextToil();
                    ticker++;
                },
                defaultCompleteMode = ToilCompleteMode.Never
            };
            wait.WithProgressBar(TargetIndex.A, () => ticker / duration);
            //unequip to inventory
            yield return wait;
            yield return unequip;
            //hold in hands
            yield return new Toil
            {
                initAction = delegate ()
                {
                    Thing thing = job.GetTarget(TargetIndex.A).Thing;
                    CompUnloadChecker c = thing.TryGetComp<CompUnloadChecker>();
                    if (c == null || !c.ShouldUnload)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    if (thing == null || !pawn.inventory.innerContainer.Contains(thing))
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || !thing.def.EverStorable(false))
                    {
                        pawn.inventory.innerContainer.TryDrop(thing, ThingPlaceMode.Near, countToDrop, out thing, null, null);
                        EndJobWith(JobCondition.Succeeded);
                    }
                    else
                    {
                        pawn.inventory.innerContainer.TryTransferToContainer(thing, pawn.carryTracker.innerContainer, countToDrop, out thing, true);
                        job.count = countToDrop;
                        job.SetTarget(TargetIndex.A, thing);
                    }
                    thing.SetForbidden(false, false);
                }
            };
            Toil carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.B).FailOnDestroyedOrNull(TargetIndex.A).FailOn(delegate () { return !stillUnloadable(pawn.CurJob.GetTarget(TargetIndex.A).Thing); });
            yield return carryToCell;
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carryToCell, true);
            if (Sidearms_Utility.Active)
                yield return new Toil
                {
                    initAction = delegate ()
                    {
                        Thing thing = job.GetTarget(TargetIndex.A).Thing;
                        Sidearms_Utility.ForgetSidearm(GetActor(), thing);
                    }
                };
            yield break;
        }

        private int countToDrop = -1;
    }

    public static class ITab_Pawn_Gear_Utility
    {
        public static PropertyInfo LCanControl = null;
        public static PropertyInfo LSelPawnForGear = null;
        public static MethodInfo LInterfaceDrop = null;
        //
    }

    [HarmonyPatch(typeof(ITab_Pawn_Gear), "DrawThingRow")]
    public static class ITab_Pawn_Gear_DrawThingRow_CommonSensePatch
    {
        public static void Prepare()
        {
            ITab_Pawn_Gear_Utility.LCanControl = AccessTools.Property(typeof(ITab_Pawn_Gear), "CanControl");
            ITab_Pawn_Gear_Utility.LSelPawnForGear = AccessTools.Property(typeof(ITab_Pawn_Gear), "SelPawnForGear");
            ITab_Pawn_Gear_Utility.LInterfaceDrop = AccessTools.Method(typeof(ITab_Pawn_Gear), "InterfaceDrop", new Type[] { typeof(Thing) });
        }
        public static bool Prefix(ITab_Pawn_Gear __instance, ref float y, ref float width, Thing thing, bool inventory)
        {
            return Utility.DrawThingRow(__instance, ref y, ref width, thing, inventory);
        }
    }
}