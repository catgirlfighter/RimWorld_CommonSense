using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CommonSense
{
    //optimazing the path to pick up items
    [HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
    static class WorkGiver_DoBill_TryStartNewDoBillJob_CommonSensePatch
    {
        internal static bool Prepare()
        {
            return !Settings.optimal_patching_in_use || Settings.adv_haul_all_ings;
        }

        internal static void Postfix(bool __result, List<ThingCount> chosen)
        {
            if (!__result || !Settings.adv_haul_all_ings)
                return;

            Utility.OptimizePath(chosen);
        }
    }

    //want to add all items in the same stockpile or a group as items picked for comparison
    [HarmonyPatch]
    internal static class WorkGiver_DoBill_TryFindBestIngredientsHelper_CommonSensePatch
    {
        internal static bool Prepare()
        {
            return !Settings.optimal_patching_in_use || Settings.prefer_spoiling_ingredients;
        }

        private static Type dc24_0;
        private static void PreProcess(Pawn pawn, Predicate<Thing> baseValidator, bool billGiverIsPawn, List<Thing> newRelevantThings, HashSet<Thing> processedThings)
        {
            if (!Settings.prefer_spoiling_ingredients || billGiverIsPawn)
                return;

            var stores = new HashSet<ISlotGroup>();
            foreach (var thing in newRelevantThings)
            {
                if (thing.TryGetComp<CompRottable>() == null) continue;
                ISlotGroup slotGroup = thing.GetSlotGroup();
                if (slotGroup == null) continue;
                ISlotGroup storGroup = thing.GetSlotGroup()?.StorageGroup;
                slotGroup = (storGroup ?? slotGroup);
                stores.Add(slotGroup);
            }

            foreach (var store in stores)
            {
                foreach (var thing in store.HeldThings)
                    if (!thing.def.IsMedicine && !processedThings.Contains(thing) && baseValidator(thing)
                        && pawn.CanReach(thing, PathEndMode.OnCell, Danger.Deadly))
                    {
                        newRelevantThings.Add(thing);
                        processedThings.Add(thing);
                    }
            }
        }

        internal static MethodBase TargetMethod()
        {
            dc24_0 = AccessTools.Inner(typeof(WorkGiver_DoBill), "<>c__DisplayClass24_0");
            MethodInfo b_4 = AccessTools.Method(typeof(WorkGiver_DoBill), "TryFindBestIngredientsHelper");
            return b_4;
        }

        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instrs)
        {
            bool b0 = false;
            FieldInfo pawn = AccessTools.Field(dc24_0, "pawn");
            FieldInfo baseValidator = AccessTools.Field(dc24_0, "baseValidator");
            FieldInfo billGiverIsPawn = AccessTools.Field(dc24_0, "billGiverIsPawn");
            //FieldInfo foundAllIngredientsAndChoose = AccessTools.Field(dc24_0, "foundAllIngredientsAndChoose");
            FieldInfo newRelevantThings = AccessTools.Field(typeof(WorkGiver_DoBill), "newRelevantThings");
            FieldInfo processedThings = AccessTools.Field(typeof(WorkGiver_DoBill), "processedThings");
            foreach (var i in (instrs))
            {
                yield return i;
                if (i.opcode == OpCodes.Stloc_3)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, pawn);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, baseValidator);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, billGiverIsPawn);
                    yield return new CodeInstruction(OpCodes.Ldsfld, newRelevantThings);
                    yield return new CodeInstruction(OpCodes.Ldsfld, processedThings);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WorkGiver_DoBill_TryFindBestIngredientsHelper_CommonSensePatch), nameof(WorkGiver_DoBill_TryFindBestIngredientsHelper_CommonSensePatch.PreProcess)));
                    b0 = true;
                }
                //Log.Message($"{i.opcode}={i.operand}");
            }
            if (!b0) Log.Warning("[Common Sense] TryFindBestIngredientsHelper patch 0 didn't work");
        }
    }

    //sorting ingredients by how close they're to rot
    [HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet_AllowMix")]
    static class WorkGiver_DoBill_TryFindBestBillIngredientsInSet_AllowMix_CommonSensePatch
    {
        internal static bool Prepare()
        {
            return !Settings.optimal_patching_in_use || Settings.prefer_spoiling_ingredients;
        }
        private static void DoSort(List<Thing> availableThings, Bill bill)
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
            bool b0 = false;
            foreach (var i in (instrs))
            {
                yield return i;
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

    //trying to make so pawns would prefer to finish off meals that are close to spoiling
    [HarmonyPatch(typeof(FoodUtility), "FoodOptimality")]
    static class FoodUtility_FoodOptimality
    {
        private static FieldInfo LFoodOptimalityEffectFromMoodCurve = null;

        internal static bool Prepare()
        {
            LFoodOptimalityEffectFromMoodCurve = AccessTools.Field(typeof(FoodUtility), "FoodOptimalityEffectFromMoodCurve");
            if (LFoodOptimalityEffectFromMoodCurve == null) Log.Message("couldn't find field FoodUtility.FoodOptimalityEffectFromMoodCurve");
            return LFoodOptimalityEffectFromMoodCurve != null
                && (!Settings.optimal_patching_in_use || Settings.prefer_spoiling_meals);
        }

        internal static void Postfix(ref float __result, Pawn eater, Thing foodSource, ThingDef foodDef)
        {

            if (!Settings.prefer_spoiling_meals)
                return;

            const float ahour = 2500f;
            //const float qday = ahour * 6f;
            const float aday = ahour * 24f;
            CompRottable compRottable = foodSource.TryGetComp<CompRottable>();
            if (compRottable != null)
            {
                float t = (compRottable.PropsRot.TicksToRotStart - compRottable.RotProgress) / aday;
                if (t > 0 && t < 3f)
                {
                    var delta = 6f - t * 2f;
                    __result += delta * delta;
                }
            }
        }
    }
}
