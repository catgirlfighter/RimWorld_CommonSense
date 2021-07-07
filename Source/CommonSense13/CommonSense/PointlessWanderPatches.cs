using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Reflection;
using UnityEngine;
using System.Linq;
using System;
using System.Reflection.Emit;


namespace CommonSense
{
    [HarmonyPatch(typeof(RCellFinder), "CanWanderToCell")]
    static class RCellFinder_CanWanderToCell_CommonSensePatch
    {
        static void Postfix(ref bool __result, IntVec3 c, IntVec3 root, Pawn pawn)
        {
            if (!__result) return;
            //
            //if (pawn.ShouldHideFromWeather()
            //    && (pawn.Position.Roofed(pawn.Map) || root.Roofed(pawn.Map)) 
            //    && !c.Roofed(pawn.Map))
            //{
            //    __result = false;
            //    return;
            //}
            //
            if (!Settings.polite_wander || pawn.Faction.HostileTo(Faction.OfPlayer)/* || !pawn.RaceProps.Humanlike*/) return;
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
        static IntVec3 FindRoofedInHomeArea(Pawn pawn)
        {
            Predicate<IntVec3> cellValidator = (IntVec3 x) => pawn.Map.areaManager.Home[x] && !PawnUtility.KnownDangerAt(x, pawn.Map, pawn) && !x.GetTerrain(pawn.Map).avoidWander && x.Standable(pawn.Map) && x.Roofed(pawn.Map);
            Predicate<Region> validator = delegate (Region x)
            {
                IntVec3 intVec;
                return x.OverlapWith(pawn.Map.areaManager.Home) > AreaOverlap.None && !x.IsForbiddenEntirely(pawn) && x.TryFindRandomCellInRegionUnforbidden(pawn, cellValidator, out intVec);
            };
            Region reg;
            if (!CellFinder.TryFindClosestRegionWith(pawn.GetRegion(RegionType.Set_Passable), TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false), validator, 100, out reg, RegionType.Set_Passable))
            {
                return IntVec3.Invalid;
            }
            //
            IntVec3 root;
            if (!reg.TryFindRandomCellInRegionUnforbidden(pawn, cellValidator, out root))
            {
                return IntVec3.Invalid;
            }

            return RCellFinder.RandomWanderDestFor(pawn, root, 7, ((Pawn p, IntVec3 v1, IntVec3 v2) => v1.Roofed(p.Map)), PawnUtility.ResolveMaxDanger(pawn, Danger.Deadly));
        }

        static bool Prefix(Pawn pawn, ref Job __result, JobGiver_Wander __instance)
        {
            if (!pawn.ShouldHideFromWeather() || pawn.Position.Roofed(pawn.Map))
                return true;

            var root = FindRoofedInHomeArea(pawn);

            if (root != IntVec3.Invalid)
            {
                Job job = JobMaker.MakeJob(JobDefOf.GotoWander, root);
                job.locomotionUrgency = LocomotionUrgency.Jog;
                job.expiryInterval = -1;
                __result = job;

                return false;
            }

            return true;
        }

        static void Postfix(Pawn pawn, ref Job __result)
        {
            if (!Settings.safe_wander
                || __result == null
                || __result.locomotionUrgency > LocomotionUrgency.Walk
                || !pawn.ShouldHideFromWeather())
                return;

            __result.targetB = new IntVec3(UnityEngine.Random.Range(400, 800), 0, 0);
        }
    }

    //[HarmonyPatch(typeof(JobDriver_Goto), "MoveNext")]
    [HarmonyPatch]
    static class JobDriver_Goto_MoveNext_CommonSensePatch
    {
        static MethodBase TargetMethod()
        {
            Type inner = AccessTools.Inner(typeof(JobDriver_Goto), "<MakeNewToils>d__1");
            return AccessTools.Method(inner, "MoveNext");
        }
        //modified copy of Pawn_PathFollower.CostToMoveIntoCell
        static int MoveCost(this Pawn pawn, IntVec3 from, IntVec3 to)
        {
            int num;
            if (to.x == from.x || to.z == from.z)
            {
                num = pawn.TicksPerMoveCardinal;
            }
            else
            {
                num = pawn.TicksPerMoveDiagonal;
            }
            num += pawn.Map.pathing.For(pawn).pathGrid.CalculatedCostAt(to, false, from);
            Building edifice = to.GetEdifice(pawn.Map);
            if (edifice != null)
            {
                num += edifice.PathWalkCostFor(pawn);
            }
            if (num > 450)
            {
                num = 450;
            }
            if (pawn.CurJob != null)
            {
                Pawn locomotionUrgencySameAs = pawn.jobs.curDriver.locomotionUrgencySameAs;
                if (locomotionUrgencySameAs != null && locomotionUrgencySameAs != pawn && locomotionUrgencySameAs.Spawned)
                {
                    int num2 = locomotionUrgencySameAs.MoveCost(from, to);
                    if (num < num2)
                    {
                        num = num2;
                    }
                }
                else
                {
                    switch (pawn.jobs.curJob.locomotionUrgency)
                    {
                        case LocomotionUrgency.Amble:
                            num *= 3;
                            if (num < 60)
                            {
                                num = 60;
                            }
                            break;
                        case LocomotionUrgency.Walk:
                            num *= 2;
                            if (num < 50)
                            {
                                num = 50;
                            }
                            break;
                        case LocomotionUrgency.Jog:
                            //num = num;
                            break;
                        case LocomotionUrgency.Sprint:
                            num = Mathf.RoundToInt((float)num * 0.75f);
                            break;
                    }
                }
            }
            return Mathf.Max(num, 1);
        }

        static Toil GoToCellSafe(TargetIndex ind, PathEndMode peMode, TargetIndex paramind)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                var param = actor.jobs.curJob.GetTarget(paramind);
                var target = actor.jobs.curJob.GetTarget(ind);
                //
                if (param == null || param.Cell == IntVec3.Invalid)
                {
                    actor.pather.StartPath(target, peMode);
                    return;
                }
                //
                var map = actor.Map;
                var path = map.pathFinder.FindPath(actor.Position, target, TraverseParms.For(actor), peMode);

                if (!path.Found)
                {
                    path.ReleaseToPool();
                    actor.pather.StartPath(target, peMode);
                    return;
                }
                //
                var maxTotalCost = param.Cell.x;
                int maxDangerCost = 200;
                int costDangerCounter = 0;
                int costTotalCounter = 0;
                int lastSafe = -1;
                IntVec3 prevCell = actor.Position;
                for (int i = path.NodesReversed.Count - 1; i >= 0; i--)
                {
                    IntVec3 cell = path.NodesReversed[i];
                    var cost = actor.MoveCost(cell, prevCell);
                    if ((costTotalCounter += cost) > maxTotalCost)
                        break;

                    if (cell.Roofed(map))
                    {
                        lastSafe = i;
                        costDangerCounter = 0;
                    }
                    else
                    {
                        if ((costDangerCounter += cost) > maxDangerCost)
                            break;
                    }
                    prevCell = cell;
                }
                //
                if (lastSafe == -1)
                {
                    path.ReleaseToPool();
                    actor.CurJob.SetTarget(ind, actor.Position);
                    actor.pather.StartPath(actor.Position, PathEndMode.OnCell);
                    return;
                }
                //
                prevCell = path.NodesReversed[lastSafe];
                Building_Door door = map.thingGrid.ThingAt<Building_Door>(prevCell);
                if (door != null && !door.FreePassage)
                    if (lastSafe == path.NodesReversed.Count - 1)
                    {
                        path.ReleaseToPool();
                        actor.CurJob.SetTarget(ind, actor.Position);
                        actor.pather.StartPath(actor.Position, PathEndMode.OnCell);
                        return;
                    }
                    else
                    {
                        lastSafe++;
                        prevCell = path.NodesReversed[lastSafe];
                    }

                path.ReleaseToPool();
                actor.CurJob.SetTarget(ind, prevCell);
                actor.pather.StartPath(prevCell, PathEndMode.OnCell);
            };
            toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            return toil;
            //return Toils_Goto.GotoCell(ind, peMode);
        }
        //
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instrs, ILGenerator il)
        {
            MethodInfo LGotoCell = AccessTools.Method(typeof(Toils_Goto), nameof(Toils_Goto.GotoCell), new Type[] { typeof(TargetIndex), typeof(PathEndMode) });
            MethodInfo LGotoCellSafe = AccessTools.Method(typeof(JobDriver_Goto_MoveNext_CommonSensePatch), nameof(JobDriver_Goto_MoveNext_CommonSensePatch.GoToCellSafe));
            foreach (var i in (instrs))
            {
                if (i.opcode == OpCodes.Call && i.operand == (object)LGotoCell)
                {
                    yield return new CodeInstruction(OpCodes.Ldc_I4_2);
                    i.operand = LGotoCellSafe;
                }

                yield return i;
            }
        }
    }
}