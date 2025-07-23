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
    [DefOf]
    public static class CommonSenseJobDefOf
    {
        public static JobDef UnloadMarkedItems;
        //public static JobDef DoBillCommonSense;
        public static JobDef SocialRelax;
        public static JobDef SocialRelaxCommonSense;
    }

    [StaticConstructorOnStartup]
    public static class Utility
    {
        public static readonly Texture2D texUnloadThing = ContentFinder<Texture2D>.Get("UI/Icons/Unload_Thing");
        public static readonly Texture2D texUnloadThingCancel = ContentFinder<Texture2D>.Get("UI/Icons/Unload_Thing_Cancel");
        public static readonly Texture2D texMoteClean = ContentFinder<Texture2D>.Get("Things/Mote/Clean");

        private static WorkGiverDef cleanFilth = null;
        private static WorkGiverDef CleanFilth { get { return cleanFilth ?? (cleanFilth = DefDatabase<WorkGiverDef>.GetNamed("CleanFilth")); } }
        public const byte largeRoomSize = 160;

        private static WorkTypeDef cleaningDef = null;
        public static WorkTypeDef CleaningDef
        {
            get
            {
                if (cleaningDef == null)
                {
                    cleaningDef = DefDatabase<WorkTypeDef>.GetNamed("Cleaning");
                }
                return cleaningDef;
            }
        }

        public static bool IncapableOfCleaning(Pawn pawn)
        {
            return pawn.def.race == null
                || pawn.RaceProps.intelligence < Intelligence.ToolUser
                //|| pawn.def.race.intelligence < Intelligence.ToolUser
                || pawn.Faction != Find.FactionManager.OfPlayer//Faction.OfPlayer
                || pawn.workSettings == null
                || !pawn.workSettings.Initialized
                || !pawn.workSettings.WorkIsActive(CleaningDef)
                || pawn.WorkTypeIsDisabled(CleaningDef)
                || pawn.InMentalState || pawn.IsBurning();
        }

        public static IEnumerable<Filth> SelectAllFilth(Pawn pawn, LocalTargetInfo target, int Limit = int.MaxValue)
        {
            Room room = null;
            if (target.Thing == null)
            {
                if((room = GridsUtility.GetRoom(target.Cell, pawn.Map)) == null)//(target.Cell == null)
                    Log.Error("Invalid target: cell or thing it must be");
                //else
                //    room = GridsUtility.GetRoom(target.Cell, pawn.Map);
             }
            else
                room = target.Thing.GetRoom();

            if (room == null)
                return new List<Filth>();

            PathGrid pathGrid = pawn.Map.pathing.For(pawn).pathGrid;
            if (pathGrid == null)
                return new List<Filth>();

            var cleanFilth = CleanFilth;
            //if (cleanFilth == null)
            //{
            //    cleanFilth = DefDatabase<JobDef>.GetNamed('Clean').driverClass; //DefDatabase<WorkGiverDef>.GetNamed("CleanFilth");
            //}

            if (cleanFilth.Worker == null)
                return new List<Filth>();

            IEnumerable<Filth> enumerable = null;
            if (room.IsHuge || room.CellCount > largeRoomSize)
            {
                enumerable = new List<Filth>();
                for (int i = 0; i < 200; i++)
                {
                    IntVec3 intVec = target.Cell + GenRadial.RadialPattern[i];
                    if (intVec.InBounds(pawn.Map) && intVec.InAllowedArea(pawn) && (intVec.GetRoom(pawn.Map) == room || intVec.GetDoor(pawn.Map) != null))
                    {
                        ((List<Filth>)enumerable)
                        .AddRange(intVec.GetThingList(pawn.Map).OfType<Filth>()
                            .Where(
                                f => !f.Destroyed
                                && ((WorkGiver_Scanner)cleanFilth.Worker).HasJobOnThing(pawn, f)
                            ).Take(Limit == 0 ? int.MaxValue : Limit)
                        );
                    }
                    if (Limit > 0 && enumerable.Count() >= Limit)
                        break;
                }
            }
            else
            {
                enumerable = room.ContainedAndAdjacentThings.OfType<Filth>().Where(delegate (Filth f)
                {
                    if (f == null || f.Destroyed || !f.Position.InAllowedArea(pawn) || !((WorkGiver_Scanner)cleanFilth.Worker).HasJobOnThing(pawn, f))
                        return false;

                    Room room2 = f.GetRoom();
                    if (room2 == null || room2 != room && !room2.IsDoorway)
                        return false;

                    return true;
                }).Take(Limit == 0 ? int.MaxValue : Limit);
            }
            return enumerable;
        }

        public static void AddFilthToQueue(Job j, TargetIndex ind, IEnumerable<Filth> l, Pawn pawn)
        {
            foreach (Filth f in (l))
                j.AddQueuedTarget(ind, f);

            OptimizePath(j.GetTargetQueue(ind), pawn);
        }

        public static void OptimizePath(List<LocalTargetInfo> q, Thing Starter)
        {
            if (q.Count > 0)
            {
                int x;// = 0;
                int idx = 0;
                int n;// = 0;
                LocalTargetInfo out_of_all_things_they_didnt_add_a_simple_swap;// = null;

                if (Starter != null)
                {
                    n = q[0].Cell.DistanceToSquared(Starter.Position);

                    for (int i = 1; i < q.Count(); i++)
                    {
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
                    n = q[i].Cell.DistanceToSquared(q[i + 1].Cell);
                    idx = i + 1;
                    for (int c = i + 2; c < q.Count(); c++)
                    {
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

        public static void OptimizePath(List<ThingCount> q, Thing Starter = null)
        {
            if (q.Count > 0)
            {
                //int x;// = 0;
                //int idx = 0;
                //int n;// = 0;
                ThingCount out_of_all_things_they_didnt_add_a_simple_swap;// = default(ThingCount);

                if (Starter != null)
                {
                    var n = /* q[0].Thing.Position == null ? int.MaxValue : */q[0].Thing.Position.DistanceToSquared(Starter.Position);
                    int idx = 0;
                    for (int i = 1; i < q.Count(); i++)
                    {
                        //if (q[i].Thing.Position == null)
                        //    continue;
                        var x = q[i].Thing.Position.DistanceToSquared(Starter.Position);
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
                    //if (q[i + 1].Thing.Position == null)
                    //    continue;

                    var n = q[i].Thing.Position.DistanceToSquared(q[i + 1].Thing.Position);
                    var idx = i + 1;
                    for (int c = i + 2; c < q.Count(); c++)
                    {
                        //if (q[c].Thing.Position == null)
                        //    continue;

                        var x = q[i].Thing.Position.DistanceToSquared(q[c].Thing.Position);
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

        public static bool ConcernedByVacuumAtAll(this Pawn pawn)
        {
            if (!ModsConfig.OdysseyActive)
            {
                return false;
            }

            if (pawn.RaceProps.IsMechanoid || (pawn.IsMutant && !pawn.mutant.Def.breathesAir))
            {
                return false;
            }

            return pawn.GetStatValue(StatDefOf.VacuumResistance, applyPostProcess: true, 60) < 100f;
        }
        public static bool ShouldHideFromWeather(this Pawn pawn)
        {
            Map map;
            if (pawn.Faction != Find.FactionManager.OfPlayer
                || !(map = pawn.Map).IsPlayerHome
                || pawn.mindState?.duty != null)
                return false;
            //
            //bool cares = pawn.needs?.mood != null;

            return
                //bad weather
                pawn.needs?.mood != null && !JoyUtility.EnjoyableOutsideNow(map)
                //toxic fallout
                || pawn.RaceProps.IsFlesh && Mathf.Max(pawn.GetStatValue(StatDefOf.ToxicResistance), pawn.GetStatValue(StatDefOf.ToxicEnvironmentResistance)) < 100f && map.gameConditionManager.ActiveConditions.Any(x => x is GameCondition_ToxicFallout)
                //vaacum
                || pawn.Map.Biome.inVacuum && pawn.ConcernedByVacuumAtAll();
        }

        public static bool IsBiocodedOrLinked(this Pawn pawn, Thing thing, bool? inventory = null)
        {
            return pawn.IsQuestLodger() && (inventory == true || !EquipmentUtility.QuestLodgerCanUnequip(thing, pawn));
        }

        public static bool IsLocked(this Pawn pawn, Thing thing)
        {
            Apparel apparel;
            return (apparel = (thing as Apparel)) != null && pawn.apparel != null && pawn.apparel.IsLocked(apparel);
        }

        public static bool DrawThingRow(object tab, ref float y, ref float width, Thing thing, bool inventory)
        {
            if (!Settings.gui_manual_unload)
                return true;

            Pawn SelPawn = (Pawn)ITab_Pawn_Gear_Utility.LSelPawnForGear.GetValue(tab);
            bool CanControl = (bool)ITab_Pawn_Gear_Utility.LCanControl.GetValue(tab);
            Color hColor = new Color(1f, 0.8f, 0.8f, 1f);
            //Log.Message($"CanControl={CanControl}, pc={SelPawn.IsColonistPlayerControlled}, spawned={SelPawn.Spawned}, pcHome={SelPawn.Map.IsPlayerHome}, bcoded={IsBiocodedOrLinked(SelPawn, thing, inventory)}, locked={IsLocked(SelPawn, thing)}");

            Rect rect = new Rect(0f, y, width, 28f);
            if ((thing is ThingWithComps)
                && CanControl
                && (inventory || SelPawn.IsColonistPlayerControlled || SelPawn.Spawned && !SelPawn.Map.IsPlayerHome)
                && !IsBiocodedOrLinked(SelPawn, thing, inventory)
                && !IsLocked(SelPawn, thing))
            {
                Rect rect2 = new Rect(rect.width - 24f, y, 24f, 24f);
                CompUnloadChecker c = CompUnloadChecker.GetChecker(thing, false, true);
                if (c.ShouldUnload)
                {
                    TooltipHandler.TipRegion(rect2, "UnloadThingCancel".Translate());

                    var cl = GUI.color;
                    if (Widgets.ButtonImage(rect2, texUnloadThingCancel, hColor))
                    {
                        SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                        c.ShouldUnload = false;

                        if (MassUtility.Capacity(SelPawn, null) < MassUtility.GearAndInventoryMass(SelPawn)
                            && thing.stackCount * thing.GetStatValue(StatDefOf.Mass, true) > 0
                            && !thing.def.destroyOnDrop)
                        {
                            ITab_Pawn_Gear_Utility.LInterfaceDrop.Invoke(tab, new object[] { thing });
                        }
                    }
                    GUI.color = cl;
                }
                else
                {
                    TooltipHandler.TipRegion(rect2, "UnloadThing".Translate());
                    if (Widgets.ButtonImage(rect2, texUnloadThing, Color.white))
                    {
                        SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                        c.ShouldUnload = true;
                    }
                }
                width -= 24f;
            }
            return true;
        }

        public static Toil ListFilth(TargetIndex ind)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Job curJob = toil.actor.jobs.curJob;
                if (curJob.GetTargetQueue(ind).NullOrEmpty())
                {
                    LocalTargetInfo target = curJob.GetTarget(ind);
                    IEnumerable<Filth> l = SelectAllFilth(toil.actor, target, Settings.adv_clean_num);
                    AddFilthToQueue(curJob, ind, l, toil.actor);
                    toil.actor.ReserveAsManyAsPossible(curJob.GetTargetQueue(ind), curJob);
                    curJob.targetQueueA.Add(target);
                }
            };
            return toil;
        }
    }
}
