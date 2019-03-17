using System.Collections.Generic;
using Harmony;
using RimWorld;
using Verse;
using Verse.AI;

namespace CommonSense
{
    class IngredientPriority
    {
        [HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
        static class WorkGiver_DoBill_TryStartNewDoBillJob_CommonSensePatch
        {
            static void Postfix(WorkGiver_DoBill __instance, bool __result, Pawn pawn, List<ThingCount> chosen)
            {
                //return;
                if (!__result || !Settings.adv_haul_all_ings)
                    return;

                Utility.OptimizePath(chosen, pawn);
            }
        }

        [HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet_AllowMix")]
        static class WorkGiver_DoBill_TryFindBestBillIngredientsInSet_AllowMix_CommonSensePatch
        {
            static bool Prefix(List<Thing> availableThings)
            {
                if (!Settings.prefer_spoiling_ingredients)
                    return true;

                availableThings.Sort(
                    delegate (Thing a, Thing b)
                    {
                        CompRottable compa = a.TryGetComp<CompRottable>();
                        CompRottable compb = b.TryGetComp<CompRottable>();
                        if (compa == null)
                            if (compb == null)
                                return 0;
                            else
                                return 1;
                        else if (compb == null)
                            return -1;
                        else
                            return (int)(compa.PropsRot.TicksToRotStart - compa.RotProgress) / 2500 - (int)(compb.PropsRot.TicksToRotStart - compb.RotProgress) / 2500;
                    }
                );

                return true;
            }
        }
    }
}
