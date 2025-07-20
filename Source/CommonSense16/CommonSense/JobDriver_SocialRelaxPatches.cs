using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace CommonSense
{
    [HarmonyPatch(typeof(JoyGiver_SocialRelax), "TryGiveJob")]
    internal static class JoyGiver_SocialRelax_TryGiveJob_CommonSensePatch
    {
        internal static bool Prepare()
        {
            return !Settings.optimal_patching_in_use || Settings.social_relax_economy || Settings.adv_cleaning_ingest;
        }

        internal static void Postfix(ref Job __result)
        {
            if (__result?.def != CommonSenseJobDefOf.SocialRelax || !Settings.social_relax_economy && !Settings.adv_cleaning_ingest)
                return;
            //
            var job = JobMaker.MakeJob(CommonSenseJobDefOf.SocialRelaxCommonSense, __result.targetA, __result.targetB, __result.targetC);
            job.count = __result.count;
            __result = job;
            //
            return;
        }

    }
    public class JobDriver_SocialRelax_CommonSense : JobDriver_SocialRelax
    {
        //basically the same thing as vanilla JoyTickCheckEnd, but also has an offset
        private static void JoyTickCheckEndOffset(Pawn pawn, float joyoffset, JoyTickFullJoyAction fullJoyAction = JoyTickFullJoyAction.EndJob, float extraJoyGainFactor = 1f, Building joySource = null)
        {
            Job curJob = pawn.CurJob;
            if (curJob.def.joyKind == null)
            {
                Log.Warning("This method can only be called for jobs with joyKind.");
                return;
            }
            if (joySource != null)
            {
                if (joySource.def.building.joyKind != null && pawn.CurJob.def.joyKind != joySource.def.building.joyKind)
                {
                    Log.ErrorOnce("Joy source joyKind and jobDef.joyKind are not the same. building=" + joySource.ToStringSafe<Building>() + ", jobDef=" + pawn.CurJob.def.ToStringSafe<JobDef>(), joySource.thingIDNumber ^ 876598732);
                }
                extraJoyGainFactor *= joySource.GetStatValue(StatDefOf.JoyGainFactor, true);
            }
            if (pawn.needs.joy == null)
            {
                pawn.jobs.curDriver.EndJobWith(JobCondition.InterruptForced);
                return;
            }
            pawn.needs.joy.GainJoy(extraJoyGainFactor * curJob.def.joyGainRate * 0.36f / 2500f, curJob.def.joyKind);
            if (curJob.def.joySkill != null)
            {
                pawn.skills.GetSkill(curJob.def.joySkill).Learn(curJob.def.joyXpPerTick, false);
            }
            if (!curJob.ignoreJoyTimeAssignment && !pawn.GetTimeAssignment().allowJoy)
            {
                pawn.jobs.curDriver.EndJobWith(JobCondition.InterruptForced);
            }
            if (pawn.needs.joy.CurLevel > (0.99f - joyoffset))
            {
                if (fullJoyAction == JoyTickFullJoyAction.EndJob)
                {
                    pawn.jobs.curDriver.EndJobWith(JobCondition.Succeeded);
                    return;
                }
                if (fullJoyAction == JoyTickFullJoyAction.GoToNextToil)
                {
                    pawn.jobs.curDriver.ReadyForNextToil();
                }
            }
        }

        private bool HasDrink()
        {
            return job.GetTarget(TargetIndex.C).HasThing;
        }

        private bool HasChair()
        {
            return job.GetTarget(TargetIndex.B).HasThing;
        }

        private Thing GatherSpotParent()
        {
            return job.GetTarget(TargetIndex.A).Thing;
        }

        private IntVec3 ClosestGatherSpotParentCell()
        {
            return GatherSpotParent().OccupiedRect().ClosestCellTo(pawn.Position);
        }

        private float workStart;
        private float workLeft;
        private float workDone;

        private Toil CleanFilth()
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Filth filth = toil.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing as Filth;
                var progQue = toil.actor.jobs.curJob.GetTargetQueue(TargetIndex.B);
                workStart = 0f;
                workDone = 0f;
                workLeft = filth.def.filth.cleaningWorkToReduceThickness * filth.thickness;
            };
            toil.tickAction = delegate ()
            {
                Filth filth = toil.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing as Filth;
                //
                float statValueAbstract = filth.Position.GetTerrain(filth.Map).GetStatValueAbstract(StatDefOf.CleaningTimeFactor, null);
                float num = toil.actor.GetStatValue(StatDefOf.CleaningSpeed, true, -1);
                if (statValueAbstract != 0f)
                {
                    num /= statValueAbstract;
                }
                //
                workStart += num;
                workDone += num;

                if (workStart > filth.def.filth.cleaningWorkToReduceThickness)
                {
                    filth.ThinFilth();
                    workStart -= filth.def.filth.cleaningWorkToReduceThickness;
                    if (filth.Destroyed)
                    {
                        toil.actor.records.Increment(RecordDefOf.MessesCleaned);
                        ReadyForNextToil();
                        return;
                    }
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithEffect(EffecterDefOf.Clean, TargetIndex.A);
            toil.WithProgressBar(TargetIndex.A,
                delegate ()
                {
                    return workDone / workLeft;
                }
                , true, -0.5f);
            toil.PlaySustainerOrSound(() => SoundDefOf.Interact_CleanFilth);
            return toil;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
           AddEndCondition(delegate
            {
                LocalTargetInfo target = GetActor().jobs.curJob.GetTarget(TargetIndex.A);
                Thing thing = target.Thing;
                if (thing == null && target.IsValid || thing is Filth)
                {
                    return JobCondition.Ongoing;
                }
                if (thing == null || !thing.Spawned || thing.Map != GetActor().Map)
                {
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing;
            });

            if (HasChair())
            {
                this.EndOnDespawnedOrNull(TargetIndex.B, JobCondition.Incompletable);
            }
            if (HasDrink())
            {
                this.FailOnDestroyedNullOrForbidden(TargetIndex.C);
                yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.OnCell).FailOnSomeonePhysicallyInteracting(TargetIndex.C);
                yield return Toils_Haul.StartCarryThing(TargetIndex.C, false, false, false);
            }

            //cleaning patch
            Toil GoToRelaxPlace = Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            if (Settings.adv_cleaning_ingest && !Utility.IncapableOfCleaning(pawn))
            {
                yield return Utility.ListFilth(TargetIndex.A);
                yield return Toils_Jump.JumpIf(GoToRelaxPlace, () => job.GetTargetQueue(TargetIndex.A).NullOrEmpty());
                Toil CleanFilthList = Toils_JobTransforms.ClearDespawnedNullOrForbiddenQueuedTargets(TargetIndex.A, null);
                yield return CleanFilthList;
                yield return Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A, true);
                yield return Toils_Jump.JumpIf(GoToRelaxPlace, () => job.GetTargetQueue(TargetIndex.A).NullOrEmpty());
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, CleanFilthList).JumpIfOutsideHomeArea(TargetIndex.A, CleanFilthList);
                //
                Toil CleanFilth = this.CleanFilth();
                CleanFilth.JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, CleanFilthList);
                CleanFilth.JumpIfOutsideHomeArea(TargetIndex.A, CleanFilthList);
                yield return CleanFilth;
                yield return Toils_Jump.Jump(CleanFilthList);
            }

            yield return GoToRelaxPlace;
            //
            float joyoffset;
            ThingDef def = job.GetTarget(TargetIndex.C).HasThing ? job.GetTarget(TargetIndex.C).Thing.def : null;
            if (Settings.social_relax_economy && def != null && def.IsIngestible)
            {
                joyoffset = def.ingestible.joy * pawn.needs.joy.tolerances.JoyFactorFromTolerance(def.ingestible.JoyKind);
                //minimum amount of time for action to perform
                float baseoffset = Mathf.Max(0.36f / 2500f * def.ingestible.baseIngestTicks, 0.36f / 2500f * 60f * 4f);
                float expected = 1f - pawn.needs.joy.CurLevel - joyoffset;
                if (expected < baseoffset)
                {
                    float expected2 = 1f - pawn.needs.joy.CurLevel - baseoffset;
                    if (expected2 < 0f)
                        joyoffset = 0f;
                    else if (expected2 < baseoffset)
                        joyoffset = baseoffset - expected2;
                    else
                        joyoffset += expected - baseoffset;
                }
            }
            else
                joyoffset = 0f;

            Toil toil = new Toil
            {
                tickIntervalAction = delegate (int delta)
                {
                    pawn.rotationTracker.FaceCell(ClosestGatherSpotParentCell());
                    pawn.GainComfortFromCellIfPossible(delta, false);
                    JoyTickCheckEndOffset(pawn, joyoffset, JoyTickFullJoyAction.GoToNextToil);
                },
                handlingFacing = true,
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = job.def.joyDuration
            };
            toil.AddFinishAction(delegate
            {
                JoyUtility.TryGainRecRoomThought(pawn);
            });
            toil.socialMode = RandomSocialMode.SuperActive;
            Toils_Ingest.AddIngestionEffects(toil, pawn, TargetIndex.C, TargetIndex.None);
            yield return toil;
            if (HasDrink())
            {
                yield return Toils_Ingest.FinalizeIngest(pawn, TargetIndex.C);
            }
            yield break;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workStart, "workStart");
            Scribe_Values.Look(ref workLeft, "workLeft");
            Scribe_Values.Look(ref workDone, "workDone");
        }
    }
    /*
    [HarmonyPatch(typeof(JobDriver_SocialRelax), "MakeNewToils")]
    public static class JobDriver_SocialRelax_MakeNewToils_CommonSensePatch
    {
        internal static bool Prepare()
        {
            return !Settings.optimal_patching_in_use || Settings.social_relax_economy || Settings.adv_cleaning_ingest;
        }
        //basically the same thing as vanilla JoyTickCheckEnd, but also has an offset
        private static void JoyTickCheckEndOffset(Pawn pawn, float joyoffset, JoyTickFullJoyAction fullJoyAction = JoyTickFullJoyAction.EndJob, float extraJoyGainFactor = 1f, Building joySource = null)
        {
            Job curJob = pawn.CurJob;
            if (curJob.def.joyKind == null)
            {
                Log.Warning("This method can only be called for jobs with joyKind.");
                return;
            }
            if (joySource != null)
            {
                if (joySource.def.building.joyKind != null && pawn.CurJob.def.joyKind != joySource.def.building.joyKind)
                {
                    Log.ErrorOnce("Joy source joyKind and jobDef.joyKind are not the same. building=" + joySource.ToStringSafe<Building>() + ", jobDef=" + pawn.CurJob.def.ToStringSafe<JobDef>(), joySource.thingIDNumber ^ 876598732);
                }
                extraJoyGainFactor *= joySource.GetStatValue(StatDefOf.JoyGainFactor, true);
            }
            if (pawn.needs.joy == null)
            {
                pawn.jobs.curDriver.EndJobWith(JobCondition.InterruptForced);
                return;
            }
            pawn.needs.joy.GainJoy(extraJoyGainFactor * curJob.def.joyGainRate * 0.36f / 2500f, curJob.def.joyKind);
            if (curJob.def.joySkill != null)
            {
                pawn.skills.GetSkill(curJob.def.joySkill).Learn(curJob.def.joyXpPerTick, false);
            }
            if (!curJob.ignoreJoyTimeAssignment && !pawn.GetTimeAssignment().allowJoy)
            {
                pawn.jobs.curDriver.EndJobWith(JobCondition.InterruptForced);
            }
            if (pawn.needs.joy.CurLevel > (0.99f - joyoffset))
            {
                if (fullJoyAction == JoyTickFullJoyAction.EndJob)
                {
                    pawn.jobs.curDriver.EndJobWith(JobCondition.Succeeded);
                    return;
                }
                if (fullJoyAction == JoyTickFullJoyAction.GoToNextToil)
                {
                    pawn.jobs.curDriver.ReadyForNextToil();
                }
            }
        }

        private static bool HasDrink(this JobDriver_SocialRelax driver)
        {
            return driver.job.GetTarget(TargetIndex.C).HasThing;
        }

        private static bool HasChair(this JobDriver_SocialRelax driver)
        {
            return driver.job.GetTarget(TargetIndex.B).HasThing;
        }

        private static Thing GatherSpotParent(this JobDriver_SocialRelax driver)
        {
            return driver.job.GetTarget(TargetIndex.A).Thing;
        }

        private static IntVec3 ClosestGatherSpotParentCell(this JobDriver_SocialRelax driver)
        {
            return driver.GatherSpotParent().OccupiedRect().ClosestCellTo(driver.pawn.Position);
        }

        private static IEnumerable<Toil> _MakeToils(this JobDriver_SocialRelax driver)
        {
            //driver.EndOnDespawnedOrNull(TargetIndex.A, JobCondition.Incompletable);
            driver.AddEndCondition(delegate
            {
                LocalTargetInfo target = driver.GetActor().jobs.curJob.GetTarget(TargetIndex.A);
                Thing thing = target.Thing;
                if (thing == null && target.IsValid || thing is Filth)
                {
                    return JobCondition.Ongoing;
                }
                if (thing == null || !thing.Spawned || thing.Map != driver.GetActor().Map)
                {
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing;
            });

            if (driver.HasChair())
            {
                driver.EndOnDespawnedOrNull(TargetIndex.B, JobCondition.Incompletable);
            }
            if (driver.HasDrink())
            {
                driver.FailOnDestroyedNullOrForbidden(TargetIndex.C);
                yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.OnCell).FailOnSomeonePhysicallyInteracting(TargetIndex.C);
                yield return Toils_Haul.StartCarryThing(TargetIndex.C, false, false, false);
            }

            //cleaning patch
            Toil GoToRelaxPlace = Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            if (Settings.adv_cleaning_ingest && !Utility.IncapableOfCleaning(driver.pawn))
            {
                Toil FilthList = new Toil();
                FilthList.initAction = delegate ()
                {
                    Job curJob = FilthList.actor.jobs.curJob;
                    if (curJob.GetTargetQueue(TargetIndex.A).NullOrEmpty())
                    {
                        LocalTargetInfo A = curJob.GetTarget(TargetIndex.A);
                        IEnumerable<Filth> l = Utility.SelectAllFilth(FilthList.actor, A, Settings.adv_clean_num);
                        Utility.AddFilthToQueue(curJob, TargetIndex.A, l, FilthList.actor);
                        FilthList.actor.ReserveAsManyAsPossible(curJob.GetTargetQueue(TargetIndex.A), curJob);
                        curJob.targetQueueA.Add(A);
                    }
                };
                yield return FilthList;
                yield return Toils_Jump.JumpIf(GoToRelaxPlace, () => driver.job.GetTargetQueue(TargetIndex.A).NullOrEmpty());
                Toil CleanFilthList = Toils_JobTransforms.ClearDespawnedNullOrForbiddenQueuedTargets(TargetIndex.A, null);
                yield return CleanFilthList;
                yield return Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A, true);
                yield return Toils_Jump.JumpIf(GoToRelaxPlace, () => driver.job.GetTargetQueue(TargetIndex.A).NullOrEmpty());
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, CleanFilthList).JumpIfOutsideHomeArea(TargetIndex.A, CleanFilthList);
                //
                driver.job.GetTargetQueue(TargetIndex.B).Add(new IntVec3(0, 0, 0));
                Toil clean = new Toil();
                clean.initAction = delegate ()
                {
                    Filth filth = clean.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing as Filth;
                    var progQue = clean.actor.jobs.curJob.GetTargetQueue(TargetIndex.B);
                    progQue[0] = new IntVec3(0, 0, (int)filth.def.filth.cleaningWorkToReduceThickness * filth.thickness * 100);
                };
                clean.tickAction = delegate ()
                {
                    Filth filth = clean.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing as Filth;
                    //
                    float statValueAbstract = filth.Position.GetTerrain(filth.Map).GetStatValueAbstract(StatDefOf.CleaningTimeFactor, null);
                    float num = clean.actor.GetStatValue(StatDefOf.CleaningSpeed, true, -1);
                    if (statValueAbstract != 0f)
                    {
                        num /= statValueAbstract;
                    }
                    //
                    var progQue = clean.actor.jobs.curJob.GetTargetQueue(TargetIndex.B);
                    IntVec3 iv = progQue[0].Cell;
                    iv.x += Mathf.Max(100, Mathf.RoundToInt(num * 100));
                    iv.y += Mathf.Max(100, Mathf.RoundToInt(num * 100));
                    if (iv.x > filth.def.filth.cleaningWorkToReduceThickness * 100)
                    {
                        filth.ThinFilth();
                        iv.x -= (int)(filth.def.filth.cleaningWorkToReduceThickness * 100);
                        if (filth.Destroyed)
                        {
                            clean.actor.records.Increment(RecordDefOf.MessesCleaned);
                            driver.ReadyForNextToil();
                            return;
                        }
                    }
                    progQue[0] = iv;
                };
                clean.defaultCompleteMode = ToilCompleteMode.Never;
                clean.WithEffect(EffecterDefOf.Clean, TargetIndex.A);
                clean.WithProgressBar(TargetIndex.A,
                    delegate ()
                    {
                        var q = driver.job.GetTargetQueue(TargetIndex.B)[0];
                        float result = (float)q.Cell.y / q.Cell.z;
                        return result;
                    }
                    , true, -0.5f);
                clean.PlaySustainerOrSound(() => SoundDefOf.Interact_CleanFilth);
                clean.JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, CleanFilthList);
                clean.JumpIfOutsideHomeArea(TargetIndex.A, CleanFilthList);
                yield return clean;
                yield return Toils_Jump.Jump(CleanFilthList);
            }

            yield return GoToRelaxPlace;
            //
            float joyoffset;
            ThingDef def = driver.job.GetTarget(TargetIndex.C).HasThing ? driver.job.GetTarget(TargetIndex.C).Thing.def : null;
            if (Settings.social_relax_economy && def != null && def.IsIngestible)
            {
                joyoffset = def.ingestible.joy * driver.pawn.needs.joy.tolerances.JoyFactorFromTolerance(def.ingestible.JoyKind);
                //minimum amount of time for action to perform
                float baseoffset = Mathf.Max(0.36f / 2500f * def.ingestible.baseIngestTicks, 0.36f / 2500f * 60f * 4f);
                float expected = 1f - driver.pawn.needs.joy.CurLevel - joyoffset;
                if (expected < baseoffset)
                {
                    float expected2 = 1f - driver.pawn.needs.joy.CurLevel - baseoffset;
                    if (expected2 < 0f)
                        joyoffset = 0f;
                    else if (expected2 < baseoffset)
                        joyoffset = baseoffset - expected2;
                    else
                        joyoffset += expected - baseoffset;
                }
            }
            else
                joyoffset = 0f;

            Toil toil = new Toil
            {
                tickIntervalAction = delegate (int delta)
                {
                    driver.pawn.rotationTracker.FaceCell(driver.ClosestGatherSpotParentCell());
                    driver.pawn.GainComfortFromCellIfPossible(delta, false);
                    JoyTickCheckEndOffset(driver.pawn, joyoffset, JoyTickFullJoyAction.GoToNextToil);
                },
                handlingFacing = true,
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = driver.job.def.joyDuration
            };
            toil.AddFinishAction(delegate
            {
                JoyUtility.TryGainRecRoomThought(driver.pawn);
            });
            toil.socialMode = RandomSocialMode.SuperActive;
            Toils_Ingest.AddIngestionEffects(toil, driver.pawn, TargetIndex.C, TargetIndex.None);
            yield return toil;
            if (driver.HasDrink())
            {
                yield return Toils_Ingest.FinalizeIngest(driver.pawn, TargetIndex.C);
            }
            yield break;
        }

        internal static bool Prefix(ref IEnumerable<Toil> __result, JobDriver_SocialRelax __instance)
        {
            if (!Settings.social_relax_economy && !Settings.adv_cleaning_ingest)
                return true;
            //
            __result = __instance._MakeToils();
            return false;
        }
    }
    */
}
