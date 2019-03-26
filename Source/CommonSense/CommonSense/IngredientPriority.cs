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

                Utility.OptimizePath(chosen);
            }
        }

        [HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet_AllowMix")]
        public static class WorkGiver_DoBill_TryFindBestBillIngredientsInSet_AllowMix_CommonSensePatch
        {
            public static bool Prefix(/*Thing billGiver, */List<Thing> availableThings)
            {
                if (!Settings.prefer_spoiling_ingredients)
                    return true;

                availableThings.Sort(
                    delegate (Thing a, Thing b)
                    {
                        float p = a.GetStatValue(StatDefOf.MedicalPotency) - b.GetStatValue(StatDefOf.MedicalPotency);
                        if (p > 0)
                            return -1;
                        else if (p < 0)
                            return 1;
                        
                        CompRottable compa = a.TryGetComp<CompRottable>();
                        CompRottable compb = b.TryGetComp<CompRottable>();

                        //int r = a.Position.DistanceToSquared(billGiver.Position) - b.Position.DistanceToSquared(billGiver.Position);
                        if (compa == null)
                            if (compb == null)
                                return 0;
                            else
                                return 1;
                        else if (compb == null)
                            return -1;
                        else
                        {
                            return (int)(compa.PropsRot.TicksToRotStart - compa.RotProgress) / 2500 - (int)(compb.PropsRot.TicksToRotStart - compb.RotProgress) / 2500;
                        }
                    }
                );

                return true;
            }
        }

        [HarmonyPatch(typeof(FoodUtility), "FoodOptimality")]
        static class FoodUtility_FoodOptimality
        {
            static void Postfix(ref float __result, Pawn eater, Thing foodSource, ThingDef foodDef, float dist, bool takingToInventory = false)
            {
                if (!Settings.prefer_spoiling_ingredients)
                    return;

                const float qday = 2500f * 6f;
                const float aday = qday * 4f;
                //const float ahour = 2500f;
                CompRottable compRottable = foodSource.TryGetComp<CompRottable>();
                if (compRottable != null)
                {
                    float t = compRottable.PropsRot.TicksToRotStart - compRottable.RotProgress;
                    if (t > 0 && t < qday * 8f)
                    {
                        __result += (float)Math.Truncate((1f + (aday * 2f - t) / qday) * 1.5f);
                        //Log.Message($"{foodSource},left={t},weight={(halfhour * 4f - t) / halfhour * 3f}");
                    }
                    
                }
            }
        }
    }
}
