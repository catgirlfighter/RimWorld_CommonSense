using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;

namespace CommonSense
{
    static class Utility
    {
        static private WorkGiverDef cleanFilth = null;
        public const byte largeRoomSize = 160;

        static private WorkTypeDef fCleaningDef = null;
        static public WorkTypeDef CleaningDef
        {
            get
            {
                if (fCleaningDef == null)
                {
                    fCleaningDef = DefDatabase<WorkTypeDef>.GetNamed("Cleaning");
                }
                return fCleaningDef;
            }
        }

        public static bool IncapableOfCleaning(Pawn pawn)
        {
            return pawn.def.race == null ||
                (int)pawn.def.race.intelligence < 2 ||
                pawn.Faction != Faction.OfPlayer ||
                (int)pawn.RaceProps.intelligence < 2 ||
                pawn.WorkTagIsDisabled(WorkTags.ManualDumb | WorkTags.Cleaning) ||
                pawn.InMentalState || pawn.IsBurning() ||
                pawn.workSettings == null || !pawn.workSettings.WorkIsActive(CleaningDef);
        }

        public static IEnumerable<Filth> SelectAllFilth(Pawn pawn, LocalTargetInfo target, int Limit = int.MaxValue)
        {
            Room room = null;
            if (target.Thing == null)
                if (target.Cell == null)
                    Log.Error("Invalid target: cell or thing it must be");
                else
                    room = GridsUtility.GetRoom(target.Cell, pawn.Map);
            else
                room = target.Thing.GetRoom();

            if (room == null)
                return new List<Filth>();

            PathGrid pathGrid = pawn.Map.pathGrid;
            if (pathGrid == null)
                return new List<Filth>();

            if (cleanFilth == null)
                cleanFilth = DefDatabase<WorkGiverDef>.GetNamed("CleanFilth");

            if (cleanFilth.Worker == null)
                return new List<Filth>();

            IEnumerable<Filth> enumerable = null;
            if (room.IsHuge || room.CellCount > largeRoomSize)
            {
                enumerable = new List<Filth>();
                for (int i = 0; i < 200; i++)
                {
                    IntVec3 intVec = target.Cell + GenRadial.RadialPattern[i];
                    if (intVec.InBounds(pawn.Map) && intVec.InAllowedArea(pawn) && intVec.GetRoom(pawn.Map) == room)
                        ((List<Filth>)enumerable).AddRange(intVec.GetThingList(pawn.Map).OfType<Filth>().Where(f => !f.Destroyed
                            && ((WorkGiver_Scanner)cleanFilth.Worker).HasJobOnThing(pawn, f)).Take(Limit == 0 ? int.MaxValue : Limit));
                    if (Limit > 0 && enumerable.Count() >= Limit)
                        break;
                }
            }
            else
                enumerable = room.ContainedAndAdjacentThings.OfType<Filth>().Where(delegate (Filth f)
                {
                    if (f == null || f.Destroyed || !f.Position.InAllowedArea(pawn) || !((WorkGiver_Scanner)cleanFilth.Worker).HasJobOnThing(pawn, f))
                        return false;

                    Room room2 = f.GetRoom();
                    if (room2 == null || room2 != room && !room2.IsDoorway)
                        return false;

                    return true;
                }).Take(Limit == 0 ? int.MaxValue : Limit);
            return enumerable;
        }

        public static void AddFilthToQueue(Job j, TargetIndex ind, IEnumerable<Filth> l, Pawn pawn)
        {
            foreach (Filth f in (l))
                j.AddQueuedTarget(ind, f);

            OptimizePath(j.GetTargetQueue(ind), pawn);
        }

        static public void OptimizePath(List<LocalTargetInfo> q, Thing Starter)
        {
            if (q.Count > 0)
            {
                int x = 0;
                int idx = 0;
                int n = 0;
                LocalTargetInfo out_of_all_things_they_didnt_add_a_simple_swap = null;

                if (Starter != null)
                {
                    if (q[0].Cell == null)
                        n = int.MaxValue;
                    else
                        n = q[0].Cell.DistanceToSquared(Starter.Position);

                    for (int i = 1; i < q.Count(); i++)
                    {
                        if (q[i].Cell == null)
                            continue;
                        x = q[i].Cell.DistanceToSquared(Starter.Position);
                        if (Math.Abs(x) < Math.Abs(n))
                        {
                            n = x;
                            idx = i;
                        }
                    }

                    if (idx != 0)
                    {
                        out_of_all_things_they_didnt_add_a_simple_swap = q[idx];
                        q[idx] = q[0];
                        q[0] = out_of_all_things_they_didnt_add_a_simple_swap;
                    }
                }

                for (int i = 0; i < q.Count() - 1; i++)
                {
                    if (q[i + 1].Cell == null)
                        continue;

                    n = q[i].Cell.DistanceToSquared(q[i + 1].Cell);
                    idx = i + 1;
                    for (int c = i + 2; c < q.Count(); c++)
                    {
                        if (q[c].Cell == null)
                            continue;

                        x = q[i].Cell.DistanceToSquared(q[c].Cell);
                        if (Math.Abs(x) < Math.Abs(n))
                        {
                            n = x;
                            idx = c;
                        }
                    }

                    if (idx != i + 1)
                    {
                        out_of_all_things_they_didnt_add_a_simple_swap = q[idx];
                        q[idx] = q[i + 1];
                        q[i + 1] = out_of_all_things_they_didnt_add_a_simple_swap;
                    }
                }
            }
        }

        static public void OptimizePath(List<ThingCount> q, Thing Starter = null)
        {
            if (q.Count > 0)
            {
                int x = 0;
                int idx = 0;
                int n = 0;
                ThingCount out_of_all_things_they_didnt_add_a_simple_swap = default(ThingCount);

                if (Starter != null)
                {
                    if (q[0].Thing.Position == null)
                        n = int.MaxValue;
                    else
                        n = q[0].Thing.Position.DistanceToSquared(Starter.Position);

                    for (int i = 1; i < q.Count(); i++)
                    {
                        if (q[i].Thing.Position == null)
                            continue;
                        x = q[i].Thing.Position.DistanceToSquared(Starter.Position);
                        if (Math.Abs(x) < Math.Abs(n))
                        {
                            n = x;
                            idx = i;
                        }
                    }
                    if (idx != 0)
                    {
                        out_of_all_things_they_didnt_add_a_simple_swap = q[idx];
                        q[idx] = q[0];
                        q[0] = out_of_all_things_they_didnt_add_a_simple_swap;
                    }
                }

                for (int i = 0; i < q.Count() - 1; i++)
                {
                    if (q[i + 1].Thing.Position == null)
                        continue;

                    n = q[i].Thing.Position.DistanceToSquared(q[i + 1].Thing.Position);
                    idx = i + 1;
                    for (int c = i + 2; c < q.Count(); c++)
                    {
                        if (q[c].Thing.Position == null)
                            continue;

                        x = q[i].Thing.Position.DistanceToSquared(q[c].Thing.Position);
                        if (Math.Abs(x) < Math.Abs(n))
                        {
                            n = x;
                            idx = c;
                        }
                    }

                    if (idx != i + 1)
                    {
                        out_of_all_things_they_didnt_add_a_simple_swap = q[idx];
                        q[idx] = q[i + 1];
                        q[i + 1] = out_of_all_things_they_didnt_add_a_simple_swap;
                    }
                }
            }
        }

        public static bool ShouldHideFromWeather(this Pawn pawn)
        {
            if (!Settings.safe_wander
                || pawn.Faction != Faction.OfPlayer
                || !pawn.Map.IsPlayerHome
                || pawn.mindState?.duty != null)
                return false;
            //
            bool cares = pawn.needs?.mood != null;
            if (cares)
            {
                if (JoyUtility.EnjoyableOutsideNow(pawn.Map))
                    return false;
            }
            else if (!pawn.RaceProps.IsFlesh || pawn.Map.gameConditionManager.ActiveConditions.FirstOrDefault(x => x is GameCondition_ToxicFallout) == null)
                return false;
            //
            //if (pawn.Position.Roofed(pawn.Map))
            //    return false;

            return true;
        }



        public static bool DrawThingRow(Pawn SelPawn, bool CanControl, ref float y, ref float width, Thing thing, bool inventory)
        {
            Color hColor = new Color(1f, 0.8f, 0.8f, 1f);

            bool IsBiocodedOrLinked(Pawn pawn, Thing athing, bool ainventory)
            {
                if (pawn.IsQuestLodger())
                {
                    if (ainventory)
                    {
                        return true;
                    }
                    else
                    {
                        CompBiocodable compBiocodable = athing.TryGetComp<CompBiocodable>();
                        if (compBiocodable != null && compBiocodable.Biocoded)
                        {
                            return true;
                        }
                        else
                        {
                            CompBladelinkWeapon compBladelinkWeapon = athing.TryGetComp<CompBladelinkWeapon>();
                            return (compBladelinkWeapon != null && compBladelinkWeapon.bondedPawn == pawn);
                        }
                    }
                }
                else
                {
                    return false;
                }
            }

            bool IsLocked(Pawn pawn, Thing athing)
            {
                Apparel apparel;
                return (apparel = (athing as Apparel)) != null && pawn.apparel != null && pawn.apparel.IsLocked(apparel);
            }

            if (!Settings.gui_manual_unload)
                return true;

            Rect rect = new Rect(0f, y, width, 28f);
            if (CanControl
                && (SelPawn.IsColonistPlayerControlled || SelPawn.Spawned && !SelPawn.Map.IsPlayerHome)
                && (thing is ThingWithComps)
                && !IsBiocodedOrLinked(SelPawn, thing, inventory)
                && !IsLocked(SelPawn, thing))
            {
                Rect rect2 = new Rect(rect.width - 24f, y, 24f, 24f);
                CompUnloadChecker c = CompUnloadChecker.GetChecker(thing, false, true);
                if (c.ShouldUnload)
                {
                    TooltipHandler.TipRegion(rect2, "UnloadThingCancel".Translate());

                    //weird shenanigans with colors
                    var cl = GUI.color;
                    if (Widgets.ButtonImage(rect2, ContentFinder<Texture2D>.Get("UI/Icons/Unload_Thing_Cancel"), hColor))
                    {
                        SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                        c.ShouldUnload = false;

                        if (MassUtility.Capacity(SelPawn, null) < MassUtility.GearAndInventoryMass(SelPawn)
                            && thing.stackCount * thing.GetStatValue(StatDefOf.Mass, true) > 0
                            && !thing.def.destroyOnDrop)
                        {
                            Thing t;
                            SelPawn.inventory.innerContainer.TryDrop(thing, SelPawn.Position, SelPawn.Map, ThingPlaceMode.Near, out t, null, null);
                        }
                    }
                    GUI.color = cl;
                }
                else
                {
                    TooltipHandler.TipRegion(rect2, "UnloadThing".Translate());
                    if (Widgets.ButtonImage(rect2, ContentFinder<Texture2D>.Get("UI/Icons/Unload_Thing")))
                    {
                        SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                        c.ShouldUnload = true;
                    }
                }
                width -= 24f;
            }
            return true;
        }
    }
}
