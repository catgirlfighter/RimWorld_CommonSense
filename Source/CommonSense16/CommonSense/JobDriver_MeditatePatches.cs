using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace CommonSense
{
    //[HarmonyPatch(typeof(JobDriver_Meditate), "<MakeNewToils>b__15_3")]
    [HarmonyPatch(typeof(JobDriver_Meditate), "MeditationTick")]
    public static class JobDriver_MeditationTick_CommonSensePatch
    {
        internal static bool Prepare()
        {
            return !Settings.optimal_patching_in_use || Settings.meditation_economy;
        }
        internal static void Postfix(JobDriver_Meditate __instance)
        {
            if (!Settings.meditation_economy || __instance?.pawn == null)
                return;
            //
            bool meditating = __instance.pawn.GetTimeAssignment() == TimeAssignmentDefOf.Meditate;
            //
            var entropy = __instance.pawn.psychicEntropy;
            var joy = __instance.pawn.needs?.joy;
            var joyKind = __instance.pawn.CurJob?.def?.joyKind;
            if (!meditating
                && (joy == null || joy.CurLevel >= 0.98f || joyKind != null && joy.tolerances?.BoredOf(joyKind) == true)
                && (entropy == null || !entropy.NeedsPsyfocus || entropy.CurrentPsyfocus == 1f))
            {
                __instance.EndJobWith(JobCondition.InterruptForced);
            }
        }
    }
}
