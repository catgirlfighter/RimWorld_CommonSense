using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace CommonSense
{   
    public class PCT: Pawn_CarryTracker
    {        
        
        public PCT(Pawn newPawn) : base(newPawn) { }
        public bool SneakyMoveToInventory(IntVec3 dropLoc, ThingPlaceMode mode, out Thing resultingThing, Action<Thing, int> placedAction = null)
        {

            CompUnloadChecker cuc;
            bool b;
            if (!Settings.put_back_to_inv || pawn.Faction != Find.FactionManager.OfPlayer || (cuc = CarriedThing.TryGetComp<CompUnloadChecker>()) == null || !cuc.WasInInventory)
            {
                b = TryDropCarriedThing(dropLoc, mode, out var r, placedAction);
                resultingThing = r;
                return b;
            }

            b = innerContainer.TryTransferToContainer(CarriedThing, pawn.inventory.innerContainer, true);
            resultingThing = CarriedThing;
            return b;
        }
    }

    public class PutBackToBackpack
    {
        //private void CleanupCurrentJob(JobCondition condition, bool releaseReservations, bool cancelBusyStancesSoft = true)
        [HarmonyPatch(typeof(Pawn_JobTracker), "CleanupCurrentJob")]
        static class Pawn_CleanupCurrentJob_CommonSensePatch
        {
            internal static bool Prepare()
            {
                return !Settings.optimal_patching_in_use || Settings.put_back_to_inv;
            }
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il, MethodBase mb)
            {
                MethodInfo m = null;
                bool b;
                foreach (var mm in typeof(Pawn_CarryTracker).GetMethods())
                {
                    if (mm.Name == "TryDropCarriedThing")
                    {
                        b = true;
                        foreach (var pp in mm.GetParameters())
                        {
                            if(pp.Name == "count")
                            {
                                b = false;
                                break;
                            }
                        }
                        if(b)
                        {
                            m = mm;
                            break;
                        }

                    }
                }
                if(m == null)
                    throw new Exception("Couldn't find TryDropCarriedThing");

                foreach (var i in instructions)
                    if (i.opcode == OpCodes.Callvirt && (MethodInfo)i.operand == m)
                        yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(PCT), nameof(PCT.SneakyMoveToInventory)));
                    else
                        yield return i;
            }
        }
    }
}
