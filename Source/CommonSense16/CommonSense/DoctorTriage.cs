using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CommonSense
{
    // WorkGiver_Tend inherits both members from WorkGiver_Scanner. Opt tending
    // into priority-based scanning so GetPriority is actually used for patients.
    [HarmonyPatch(typeof(WorkGiver_Scanner), "get_Prioritized")]
    static class WorkGiver_Tend_Prioritized_CommonSensePatch
    {
        internal static void Postfix(WorkGiver_Scanner __instance, ref bool __result)
        {
            if (__instance is WorkGiver_Tend)
                __result = true;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_Scanner), "GetPriority", new[] { typeof(Pawn), typeof(TargetInfo) })]
    static class WorkGiver_Tend_GetPriority_CommonSensePatch
    {
        internal static void Postfix(WorkGiver_Scanner __instance, TargetInfo t, ref float __result)
        {
            if (!(__instance is WorkGiver_Tend) || !(t.Thing is Pawn patient) || patient.health == null)
                return;

            int ticks = HealthUtility.TicksUntilDeathDueToBloodLoss(patient);
            if (ticks < 60000)
                __result += Mathf.Clamp01(1f - ticks / 60000f) * 1000f;
        }
    }
}
