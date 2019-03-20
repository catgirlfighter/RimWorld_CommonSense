using System;
using System.Collections.Generic;
using Harmony;
using RimWorld;
using Verse;
using Verse.AI;

namespace CommonSense
{
    public static class IngredientPriority
    {
        [HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
        public static class WorkGiver_DoBill_TryStartNewDoBillJob_CommonSensePatch
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
        public static class WorkGiver_DoBill_TryFindBestBillIngredientsInSet_AllowMix_CommonSensePatch
        {
            public static bool Prefix(List<Thing> availableThings)
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
                //Log.Message("things");
                //foreach (var i in (availableThings))
                //{
                //    Log.Message($"thing={i}, counter={(i.TryGetComp<CompRottable>() == null ? -1 : i.TryGetComp<CompRottable>().PropsRot.TicksToRotStart - i.TryGetComp<CompRottable>().RotProgress) / 2500}");
                //}

                return true;
            }
        }

        [HarmonyPatch(typeof(FoodUtility), "FoodOptimality")]
        static class FoodUtility_FoodOptimality
        {
            static void Postfix(float __result, Pawn eater, Thing foodSource, ThingDef foodDef, float dist, bool takingToInventory = false)
            {
                if (!Settings.prefer_spoiling_ingredients)
                    return;

                const float halfday = 2500f * 12f;
                const float ahour = 2500f;
                CompRottable compRottable = foodSource.TryGetComp<CompRottable>();
                if (compRottable != null)
                {
                    float t = compRottable.PropsRot.TicksToRotStart - compRottable.RotProgress;
                    if (t > 0 && t < halfday * 4f)
                    {
                        __result += (float)Math.Truncate((1f + (halfday * 4f - t) / ahour) * 0.5f);
                        //Log.Message($"{foodSource},left={t},weight={(halfhour * 4f - t) / halfhour * 3f}");
                    }
                    
                }
            }
        }
    }
}
