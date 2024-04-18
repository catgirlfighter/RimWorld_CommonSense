using System;
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
            internal static void Postfix(WorkGiver_DoBill __instance, bool __result, Pawn pawn, List<ThingCount> chosen)
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

            private static void DoSort(List<Thing> availableThings, Bill bill)
            {
                if (!Settings.prefer_spoiling_ingredients || bill.recipe.addsHediff != null)
                    return;

                var stores = new HashSet<ISlotGroup>();
                foreach (var thing in availableThings)
                {
                    if (thing.TryGetComp<CompRottable>() == null) continue;
                    ISlotGroup slotGroup = thing.GetSlotGroup();
                    if (slotGroup == null) continue;
                    ISlotGroup storGroup = thing.GetSlotGroup()?.StorageGroup;
                    slotGroup = (storGroup ?? slotGroup);
                    stores.Add(slotGroup);
                }

                availableThings.RemoveAll(thing => thing.GetSlotGroup() != null && thing.TryGetComp<CompRottable>() != null);
                foreach (var store in stores)
                {
                    foreach (var thing in store.HeldThings)
                        if (bill.recipe.IsIngredient(thing.def))
                            availableThings.Add(thing);
                }

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
                bool b0 = false;
                //MethodInfo LSortBy = AccessTools.Method(typeof(GenCollection), "SortBy", new Type[] { typeof(List<Thing>), typeof(Func<Thing,Single>), typeof(Func<Thing,int>) });
                foreach (var i in (instrs))
                {
                    yield return i;
                    //Log.Message($"{i.opcode}={i.operand}");
                    if (i.opcode == OpCodes.Call && (MethodInfo)i.operand != null && ((MethodInfo)i.operand).Name == "SortBy")
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Ldarg_1);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WorkGiver_DoBill_TryFindBestBillIngredientsInSet_AllowMix_CommonSensePatch), nameof(WorkGiver_DoBill_TryFindBestBillIngredientsInSet_AllowMix_CommonSensePatch.DoSort)));
                        b0 = true;
                    }
                }
                if (!b0) Log.Warning("[Common Sense] TryFindBestBillIngredientsInSet_AllowMix patch 0 didn't work");
            }
        }

        [HarmonyPatch(typeof(FoodUtility), "FoodOptimality")]
        public static class FoodUtility_FoodOptimality
        {
            private static FieldInfo LFoodOptimalityEffectFromMoodCurve = null;

            public static void Prepare()
            {
                LFoodOptimalityEffectFromMoodCurve = AccessTools.Field(typeof(FoodUtility), "FoodOptimalityEffectFromMoodCurve");
            }

            public static void Postfix(ref float __result, Pawn eater, Thing foodSource, ThingDef foodDef, float dist, bool takingToInventory = false)
            {

                if (!Settings.prefer_spoiling_meals)
                    return;

                const float qday = 2500f * 6f;
                const float aday = qday * 4f;
                CompRottable compRottable = foodSource.TryGetComp<CompRottable>();
                if (compRottable != null)
                {
                    float t = compRottable.PropsRot.TicksToRotStart - compRottable.RotProgress;

                    float num = 0;
                    if (eater.needs != null && eater.needs.mood != null)
                    {
                        List<FoodUtility.ThoughtFromIngesting> list = FoodUtility.ThoughtsFromIngesting(eater, foodSource, foodDef);
                        for (int i = 0; i < list.Count; i++)
                        {
                            num += ((SimpleCurve)LFoodOptimalityEffectFromMoodCurve.GetValue(null)).Evaluate(list[i].thought.stages[0].baseMoodEffect);
                        }
                    }
                    //
                    if (num < 6f)
                        num = 6f;
                    //
                    if (t > 0 && t < aday * 2f)
                    {
                        __result += (float)Math.Truncate(num * (1f + (aday * 2f - t) / qday) * 0.5f);
                    }
                    //Log.Message($"{foodSource}={__result}({Math.Truncate(num * (1f + (aday * 2f - t) / qday) * 0.4f)})");
                }
            }
        }

    }
}
