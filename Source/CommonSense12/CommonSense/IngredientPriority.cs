﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
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

            static void doSort(List<Thing> availableThings, Bill bill)
            {
                if (!Settings.prefer_spoiling_ingredients || bill.recipe.addsHediff != null)
                    return;

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
            }

            [HarmonyTranspiler]
            internal static IEnumerable<CodeInstruction> AddSort(IEnumerable<CodeInstruction> instrs)
            {
                foreach (var i in (instrs))
                {
                    yield return i;

                    if (i.opcode == OpCodes.Callvirt && (MethodInfo)i.operand == typeof(List<Thing>).GetMethod(nameof(List<Thing>.Sort), new Type[] { typeof(Comparison<Thing>) } ))
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Ldarg_1);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WorkGiver_DoBill_TryFindBestBillIngredientsInSet_AllowMix_CommonSensePatch), nameof(WorkGiver_DoBill_TryFindBestBillIngredientsInSet_AllowMix_CommonSensePatch.doSort)));
                    }
                }
            }
        }

        [HarmonyPatch(typeof(FoodUtility), "FoodOptimality")]
        static class FoodUtility_FoodOptimality
        {
            static void Postfix(ref float __result, Pawn eater, Thing foodSource, ThingDef foodDef, float dist, bool takingToInventory = false)
            {

                if (!Settings.prefer_spoiling_meals)
                    return;

                const float qday = 2500f * 6f;
                const float aday = qday * 4f;
                CompRottable compRottable = foodSource.TryGetComp<CompRottable>();
                if (compRottable != null)
                {
                    float t = compRottable.PropsRot.TicksToRotStart - compRottable.RotProgress;
                    if (t > 0 && t < aday * 2f)
                    {
                        __result += (float)Math.Truncate((1f + (aday * 2f - t) / qday) * 3f);
                    }
                }
            }
        }

    }
}
