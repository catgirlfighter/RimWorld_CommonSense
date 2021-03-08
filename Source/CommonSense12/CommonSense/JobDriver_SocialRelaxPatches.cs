using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace CommonSense
{
    [HarmonyPatch(typeof(JobDriver_SocialRelax), "MakeNewToils")]
    static class JobDriver_SocialRelax_TryMakePreToilReservations_CommonSensePatch
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
            if (pawn.needs.joy.CurLevel > (0.9999f - joyoffset))
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
            driver.EndOnDespawnedOrNull(TargetIndex.A, JobCondition.Incompletable);
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
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            Toil toil = new Toil();
            float joyoffset;
            if (driver.HasDrink() && driver.job.GetTarget(TargetIndex.C).Thing.def.IsIngestible)
                joyoffset = driver.job.GetTarget(TargetIndex.C).Thing.def.ingestible.joy;
            else
                joyoffset = 0f;
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
            if (!Settings.social_relax_economy)
                return true;
            //
            __result = __instance._MakeToils();
            return false;
        }
    }
}
