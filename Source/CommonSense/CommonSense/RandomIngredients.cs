using System;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using Verse;
using RimWorld;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using UnityEngine;

namespace CommonSense
{
    class RandomIngredients
    {
        //public static Thing MakeThing(ThingDef def, ThingDef stuff = null)
        [HarmonyPatch(typeof(ThingMaker), "MakeThing", new Type[] { typeof(ThingDef), typeof(ThingDef) })]
        static class ThingMaker_MakeThing_CommonSensePatch
        {
            public static Dictionary<ThingDef, RecipeDef> hTable = new Dictionary<ThingDef, RecipeDef>();
            static void Postfix(Thing __result, ThingDef def, ThingDef stuff)
            {
                if (!Settings.add_meal_ingredients || __result == null || stuff != null)
                    return;

                CompIngredients ings = __result.TryGetComp<CompIngredients>();
                if (ings == null || ings.ingredients.Count > 0)
                    return;

                RecipeDef d = hTable.TryGetValue(def);
                if (d == null)
                {
                    d = DefDatabase<RecipeDef>.AllDefsListForReading.Where(x => x.products.Any(y => y.thingDef == def)).RandomElement();
                    if (d == null)
                        return;
                    hTable.Add(def, d);
                }

                foreach (IngredientCount c in d.ingredients)
                {
                    ThingDef td = c.filter.AllowedThingDefs.Where(
                        x => !x.IsIngestible || x.thingClass != typeof(ThingWithComps) || !FoodUtility.IsHumanlikeMeat(x) && x.ingestible.specialThoughtAsIngredient == null
                    ).RandomElement();
                    if (td != null)
                        ings.RegisterIngredient(td);
                }
            }
        }

        [HarmonyPatch]
        static class GenRecipe_MakeRecipeProducts_CommonSensePatch
        {
            private static FieldInfo field;

            internal static void ClearIngs(CompIngredients ings)
            {
                ings.ingredients.Clear();
            }

            internal static MethodBase TargetMethod()
            {
                Type nestedTypeResult = null;
                const string targetMethod = "MakeRecipeProducts";

                foreach (var nestedType in typeof(GenRecipe).GetNestedTypes(AccessTools.all))
                {
                    if (!nestedType.Name.Contains(targetMethod)) continue;

                    nestedTypeResult = nestedType;
                }

                if (nestedTypeResult == null) throw new Exception($"Could not find {targetMethod} Iterator Class");

                if (field == null)
                {
                    var fields = AccessTools.GetDeclaredFields(nestedTypeResult);
                    foreach (var i in fields)
                    {
                        if (i.FieldType == typeof(CompIngredients))
                            if (field == null)
                                field = i;
                            else
                                throw new Exception("Multiple CompIngredients fields found");
                    }
                }

                var result = AccessTools.Method(nestedTypeResult, "MoveNext");

                if (result == null) throw new Exception($"Could not find MoveNext in {nestedTypeResult.FullName}");

                return result;
            }

            [HarmonyTranspiler]
            internal static IEnumerable<CodeInstruction> CleanIngList(IEnumerable<CodeInstruction> instrs)
            {

                foreach (CodeInstruction instr in instrs)
                {

                    if (instr.opcode == OpCodes.Stfld && instr.operand == field)
                    {
                        FileLog.Log("Found");
                    }
                    yield return instr;
                    /*
                                            if (instr.operand is Label)
                                            {
                                                l1 = instr;
                                                l2 = l1;
                                            }

                                            if (instr.opcode == OpCodes.Callvirt && instr.operand == typeof(CompIngredients).GetMethod(nameof(CompIngredients.RegisterIngredient)))
                                                break;
                    */
                    //                    }
                    /*
                                        foreach (CodeInstruction instr in instrs)
                                        {
                                            yield return instr;
                                            if (instr == l2)
                                            {
                                                file.WriteLine("addedline");
                                                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GenRecipe_MakeRecipeProducts_CommonSensePatch), nameof(GenRecipe_TryDispenseFood_CommonSensePatch.ClearIngs), new Type[] { typeof(CompIngredients) }));
                                            }
                                        }
                    */
                }
            }
        }

        //public virtual Thing TryDispenseFood()
        [HarmonyPatch(typeof(Building_NutrientPasteDispenser), "TryDispenseFood")]
        static class GenRecipe_TryDispenseFood_CommonSensePatch
        {
            public static void ClearIngs(CompIngredients ings)
            {
                ings.ingredients.Clear();
            }

            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> CleanIngList(IEnumerable<CodeInstruction> instrs)
            {
                //Byte stage = 0;

                //using (StreamWriter file = new StreamWriter(@"d:\Games\steam\steamapps\common\RimWorld\Mods\CommonSense\Source\CommonSense\log.txt" + Guid.NewGuid().ToString(), true))
                //{
                //    file.WriteLine("Lines=" + instrs.Count().ToString());
                    foreach (CodeInstruction instr in instrs)
                    {
                        yield return instr;
                        if(instr.opcode == OpCodes.Stloc_S && instr.operand is LocalBuilder && ((LocalBuilder)instr.operand).LocalType == typeof(CompIngredients))
                        {
                            yield return new CodeInstruction(OpCodes.Ldloc_S, instr.operand);
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GenRecipe_MakeRecipeProducts_CommonSensePatch), nameof(GenRecipe_TryDispenseFood_CommonSensePatch.ClearIngs), new Type[] { typeof(CompIngredients) } ));
                        }
                    }
                //}
            }
        }
    }   
}
