using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Reflection;
using UnityEngine;
using System.Linq;
using System;


namespace CommonSense
{
    [HarmonyPatch(typeof(RCellFinder), "CanWanderToCell")]
    static class RCellFinder_CanWanderToCell_CommonSensePatch
    {
        static void Postfix(ref bool __result, IntVec3 c, IntVec3 root, Pawn pawn)
        {
            if (!__result )return;
            //
            if (pawn.Faction == Faction.OfPlayer 
                && (pawn.Position.Roofed(pawn.Map) || root.Roofed(pawn.Map)) 
                && !c.Roofed(pawn.Map) 
                && !JoyUtility.EnjoyableOutsideNow(pawn.Map))
            {
                __result = false;
                return;
            }
            //
            if (!pawn.RaceProps.Humanlike) return;
            //
            RoomRoleDef def = c.GetRoom(pawn.Map)?.Role;
            if (def == RoomRoleDefOf.Bedroom && !pawn.GetRoom().Owners.Contains(pawn)
            || def == RoomRoleDefOf.Hospital
            || def == RoomRoleDefOf.PrisonCell
            || def == RoomRoleDefOf.PrisonBarracks)
            {
                __result = false;
                return;
            }
        }
    }

    [HarmonyPatch(typeof(JobGiver_Wander), "TryGiveJob")]
    static class JobGiver_Wander_TryGiveJob_CommonSensePatch
    {
        static bool Prefix(Pawn pawn, ref Job __result, JobGiver_Wander __instance)
        {
            if (pawn.Faction != Faction.OfPlayer 
                || JoyUtility.EnjoyableOutsideNow(pawn.Map)
                || pawn.Position.Roofed(pawn.Map))
                return true;


            Predicate<IntVec3> cellValidator = (IntVec3 x) => !PawnUtility.KnownDangerAt(x, pawn.Map, pawn) && !x.GetTerrain(pawn.Map).avoidWander && x.Standable(pawn.Map) && x.Roofed(pawn.Map);
            Predicate<Region> validator = delegate (Region x)
            {
                IntVec3 intVec;
                return x.OverlapWith(pawn.Map.areaManager.Home) > AreaOverlap.None /*&& !x.Room.PsychologicallyOutdoors*/ && !x.IsForbiddenEntirely(pawn) && x.TryFindRandomCellInRegionUnforbidden(pawn, cellValidator, out intVec);
            };
            Region reg;
            if (!CellFinder.TryFindClosestRegionWith(pawn.GetRegion(RegionType.Set_Passable), TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false), validator, 100, out reg, RegionType.Set_Passable))
            {
                return true;
            }
            //
            IntVec3 root;
            if (!reg.TryFindRandomCellInRegionUnforbidden(pawn, cellValidator, out root))
            {
                return true;
            }

            root = RCellFinder.RandomWanderDestFor(pawn, root, 7, ((Pawn p, IntVec3 v1, IntVec3 v2) => true), PawnUtility.ResolveMaxDanger(pawn, Danger.Deadly));

            Job job = JobMaker.MakeJob(JobDefOf.GotoWander, root);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = -1;
            __result = job;

            return false;
        }

        static void Postfix(ref Job __result)
        {
            if (__result == null || __result.locomotionUrgency > LocomotionUrgency.Walk) return;
            __result.targetB = new IntVec3(UnityEngine.Random.Range(200, 600), 0, 0);
        }
    }

    [HarmonyPatch(typeof(JobDriver_Goto), "MakeNewToils")]
    static class JobDriver_Goto_MakeNewToils_CommonSensePatch
    {
        static void Postfix(JobDriver_Goto __instance, ref IEnumerable<Toil> __result)
        {
            if (__instance?.job == null) return;
            if (__instance.job.targetB == LocalTargetInfo.Invalid || __instance.pawn?.Faction != Faction.OfPlayer) return;
            List<Toil> l = __result.ToList();
            l[0].AddPreTickAction(delegate
            {
                Pawn a = __instance.GetActor();
                if (a.Faction != Faction.OfPlayer) return;
                IntVec3 val = a.jobs.curJob.GetTarget(TargetIndex.B).Cell;
                if (val == IntVec3.Invalid) return;
                val.y += 1;
                //
                if (a.pather.Moving && a.pather.Destination != a.pather.nextCell)
                    if (val.y >= val.x) a.pather.StartPath(a.pather.nextCell, PathEndMode.OnCell);
                    else if (a.pather.curPath != null && val.z != a.pather.curPath.NodesLeftCount)
                    {
                        val.z = a.pather.curPath.NodesLeftCount;
                        if (!JoyUtility.EnjoyableOutsideNow(a.Map) && a.Position.Roofed(a.Map) && !a.pather.nextCell.Roofed(a.Map))
                            a.pather.StartPath(a.Position, PathEndMode.OnCell);
                    }
                //
                a.jobs.curJob.SetTarget(TargetIndex.B, val);
            });
            __result = l;
        }
    }
}