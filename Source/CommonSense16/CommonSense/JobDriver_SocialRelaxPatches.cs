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
                yield return Utility.ListFilthToil(TargetIndex.A);
                yield return Toils_Jump.JumpIf(GoToRelaxPlace, () => job.GetTargetQueue(TargetIndex.A).NullOrEmpty());
                Toil CleanFilthList = Toils_JobTransforms.ClearDespawnedNullOrForbiddenQueuedTargets(TargetIndex.A, null);
                yield return CleanFilthList;
                yield return Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A, true);
                yield return Toils_Jump.JumpIf(GoToRelaxPlace, () => job.GetTargetQueue(TargetIndex.A).NullOrEmpty());
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, CleanFilthList).JumpIfOutsideHomeArea(TargetIndex.A, CleanFilthList);
                //
                Toil CleanFilth = Utility.CleanFilthToil(TargetIndex.A).JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, CleanFilthList).JumpIfOutsideHomeArea(TargetIndex.A, CleanFilthList);
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
    }
}
