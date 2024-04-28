using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;
using RimWorld;
using System.Reflection;
using System.Reflection.Emit;

namespace CommonSense
{

    //public static Thing MakeThing(ThingDef def, ThingDef stuff = null)
    [HarmonyPatch(typeof(ThingMaker), "MakeThing", new Type[] { typeof(ThingDef), typeof(ThingDef) })]
    static class ThingMaker_MakeThing_CommonSensePatch
    {
        private static readonly Dictionary<ThingDef, RecipeDef> hTable = new Dictionary<ThingDef, RecipeDef>();

        internal static void Postfix(Thing __result, ThingDef def)
        {
            if (!Settings.add_meal_ingredients || __result == null || !__result.def.IsIngestible)
                return;

            CompIngredients ings = __result.TryGetComp<CompIngredients>();

            //if (ings != null)
            //Log.Message($"{__result} x{__result.stackCount}, {def}, {stuff}");
            if (ings == null || ings.ingredients.Count > 0)
                return;

            RecipeDef d = hTable.TryGetValue(def);
            if (d == null)
            {
                List<RecipeDef> l = DefDatabase<RecipeDef>.AllDefsListForReading;
                if (l == null)
                    return;

                d = l.Where(x => !x.ingredients.NullOrEmpty() && x.products.Any(y => y.thingDef == def)).RandomElement();

                if (d == null)
                    return;

                hTable.Add(def, d);
            }
            foreach (IngredientCount c in d.ingredients)
            {
                ThingFilter ic = c.filter;

                if (ic == null)
                    return;

                IEnumerable<ThingDef> l = ic.AllowedThingDefs;

                if (l == null)
                    return;

                l = l.Where(
                    x => x.IsIngestible && x.comps != null && !x.comps.Any(y => y.compClass == typeof(CompIngredients))
                    && FoodUtility.GetMeatSourceCategory(x) != MeatSourceCategory.Humanlike
                    && (x.ingestible.specialThoughtAsIngredient == null || x.ingestible.specialThoughtAsIngredient.stages == null || x.ingestible.specialThoughtAsIngredient.stages[0].baseMoodEffect >= 0)
                    && (x.ingredient == null || x.ingredient.mergeCompatibilityTags.NullOrEmpty())
                );

                ThingDef td = null;
                if (l.Count() > 0)
                    td = l.RandomElement();

                if (td != null)
                    ings.RegisterIngredient(td);
            }
        }
    }

    //public static IEnumerable<Thing> MakeRecipeProducts(RecipeDef recipeDef, Pawn worker, List<Thing> ingredients, Thing dominantIngredient, IBillGiver billGiver)
    [HarmonyPatch]
    static class GenRecipe_MakeRecipeProducts_CommonSensePatch
    {
        //private static FieldInfo ingredientsCompField;

        private static void ClearIngs(CompIngredients ings)
        {
            ings?.ingredients.Clear();
        }

        internal static MethodBase TargetMethod()
        {
            Type nestedTypeResult = null;
            const string targetMethod = "<MakeRecipeProducts>";
            foreach (var nestedType in typeof(GenRecipe).GetNestedTypes(AccessTools.all))
            {
                if (!nestedType.Name.Contains(targetMethod)) continue;
                nestedTypeResult = nestedType;
            }

            if (nestedTypeResult == null) throw new Exception($"Could not find {targetMethod} Iterator Class");

            var result = AccessTools.Method(nestedTypeResult, "MoveNext");

            return result == null ? throw new Exception($"Could not find MoveNext in {nestedTypeResult.FullName}") : (MethodBase)result;
        }

        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> CleanIngList(IEnumerable<CodeInstruction> instrs)
        {

            foreach (var i in (instrs))
            {
                yield return i;

                if (i.opcode == OpCodes.Stloc_S && i.operand is LocalBuilder builder && builder.LocalType == typeof(CompIngredients))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_S, i.operand);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GenRecipe_MakeRecipeProducts_CommonSensePatch), nameof(ClearIngs), new Type[] { typeof(CompIngredients) }));
                }
            }
        }
    }

    //public virtual Thing TryDispenseFood()
    [HarmonyPatch(typeof(Building_NutrientPasteDispenser), "TryDispenseFood")]
    class GenRecipe_TryDispenseFood_CommonSensePatch
    {
        private static void ClearIngs(CompIngredients ings)
        {
            ings.ingredients.Clear();
        }

        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> CleanIngList(IEnumerable<CodeInstruction> instrs)
        {
            foreach (CodeInstruction i in instrs)
            {
                yield return i;
                if (i.opcode == OpCodes.Stloc_S && i.operand is LocalBuilder builder && builder.LocalType == typeof(CompIngredients))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_S, i.operand);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GenRecipe_MakeRecipeProducts_CommonSensePatch), nameof(ClearIngs), new Type[] { typeof(CompIngredients) }));
                }
            }
        }
    }

    [HarmonyPatch(typeof(Thing), "SplitOff", new Type[] { typeof(int) })]
    static class Thing_SplitOff_CommonSensePatch
    {
        private static void ClearIngs(Thing thing)
        {
            CompIngredients comp = thing.TryGetComp<CompIngredients>();
            comp?.ingredients.Clear();
        }

        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> CleanIngList(IEnumerable<CodeInstruction> instrs)
        {
            CodeInstruction prei = null;
            foreach (var i in (instrs))
            {
                yield return i;

                if (i.opcode == OpCodes.Stloc_0 && prei.opcode == OpCodes.Call && (MethodInfo)prei.operand == typeof(ThingMaker).GetMethod(nameof(ThingMaker.MakeThing)))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Thing_SplitOff_CommonSensePatch), nameof(ClearIngs), new Type[] { typeof(Thing) }));
                }
                prei = i;
            }
        }
    }
}
