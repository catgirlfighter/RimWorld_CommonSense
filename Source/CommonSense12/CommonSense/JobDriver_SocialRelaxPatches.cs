using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace CommonSense
{
    [HarmonyPatch(typeof(JobDriver_SocialRelax), "MakeNewToils")]
    static class JobDriver_SocialRelax_MakeNewToils_CommonSensePatch
    {
        //basically the same thing as vanilla JoyTickCheckEnd, but also has an offset
        public static void JoyTickCheckEndOffset(Pawn pawn, float joyoffset, JoyTickFullJoyAction fullJoyAction = JoyTickFullJoyAction.EndJob, float extraJoyGainFactor = 1f, Building joySource = null)
        {
            Job curJob = pawn.CurJob;
            if (curJob.def.joyKind == null)
            {
                Log.Warning("This method can only be called for jobs with joyKind.", false);
                return;
            }
            if (joySource != null)
            {
                if (joySource.def.building.joyKind != null && pawn.CurJob.def.joyKind != joySource.def.building.joyKind)
                {
                    Log.ErrorOnce("Joy source joyKind and jobDef.joyKind are not the same. building=" + joySource.ToStringSafe<Building>() + ", jobDef=" + pawn.CurJob.def.ToStringSafe<JobDef>(), joySource.thingIDNumber ^ 876598732, false);
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

        static bool HasDrink(this JobDriver_SocialRelax driver)
        {
            return driver.job.GetTarget(TargetIndex.C).HasThing;
        }

        static bool HasChair(this JobDriver_SocialRelax driver)
        {
            return driver.job.GetTarget(TargetIndex.B).HasThing;
        }

        static Thing GatherSpotParent(this JobDriver_SocialRelax driver)
        {
            return driver.job.GetTarget(TargetIndex.A).Thing;
        }

        static IntVec3 ClosestGatherSpotParentCell(this JobDriver_SocialRelax driver)
        {
            return driver.GatherSpotParent().OccupiedRect().ClosestCellTo(driver.pawn.Position);
        }

        static IEnumerable<Toil> _MakeToils(this JobDriver_SocialRelax driver)
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
                    //x = billStartTick, y = ticksSpentDoingRecipeWork, z = workLeft;
                    progQue[0] = new IntVec3(0, 0, (int)filth.def.filth.cleaningWorkToReduceThickness * filth.thickness);
                    //driver.billStartTick = 0;
                    //driver.ticksSpentDoingRecipeWork = 0;
                    //driver.workLeft = filth.def.filth.cleaningWorkToReduceThickness * filth.thickness;
                };
                clean.tickAction = delegate ()
                {
                    Filth filth = clean.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing as Filth;
                    var progQue = clean.actor.jobs.curJob.GetTargetQueue(TargetIndex.B);
                    //driver.billStartTick += 1;
                    //driver.ticksSpentDoingRecipeWork += 1;
                    IntVec3 iv = progQue[0].Cell;
                    iv.x += 1;
                    iv.y += 1;
                    if (iv.x > filth.def.filth.cleaningWorkToReduceThickness)
                    {
                        filth.ThinFilth();
                        iv.x = 0;
                        progQue[0] = iv;
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
                clean.WithProgressBar(TargetIndex.A, () => driver.job.GetTargetQueue(TargetIndex.B)[0].Cell.y / driver.job.GetTargetQueue(TargetIndex.B)[0].Cell.z, true, -0.5f);
                clean.PlaySustainerOrSound(() => SoundDefOf.Interact_CleanFilth);
                clean.JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, FilthList);
                clean.JumpIfOutsideHomeArea(TargetIndex.A, FilthList);
                yield return clean;
                yield return Toils_Jump.Jump(FilthList);
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

            Toil toil = new Toil();
            toil.tickAction = delegate ()
            {
                driver.pawn.rotationTracker.FaceCell(driver.ClosestGatherSpotParentCell());
                driver.pawn.GainComfortFromCellIfPossible(false);
                JoyTickCheckEndOffset(driver.pawn, joyoffset, JoyTickFullJoyAction.GoToNextToil);
            };
            toil.handlingFacing = true;
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = driver.job.def.joyDuration;
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

        static bool Prefix(ref IEnumerable<Toil> __result, JobDriver_SocialRelax __instance)
        {
            if (!Settings.social_relax_economy && !Settings.adv_cleaning_ingest)
                return true;
            //
            __result = __instance._MakeToils();
            return false;
        }
    }
}
