using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;

namespace CommonSense
{
    public class TextChanges
    {
        [HarmonyPatch(typeof(ThingFilter), "SetAllowAllWhoCanMake")]
        static class ThingFilter_SetAllowAllWhoCanMake_CommonSensePatch
        {
            internal static bool Prepare()
            {
                return !Settings.optimal_patching_in_use || Settings.gui_extended_recipe;
            }
            internal static bool Prefix(ThingFilter __instance, ThingDef thing)
            {
                List<ThingDef> allowAllWhoCanMake = Traverse.Create(__instance).Field("allowAllWhoCanMake").GetValue<List<ThingDef>>();
                if (allowAllWhoCanMake == null)
                {
                    allowAllWhoCanMake = new List<ThingDef>();
                    Traverse.Create(__instance).Field("allowAllWhoCanMake").SetValue(allowAllWhoCanMake);
                    allowAllWhoCanMake.Add(thing);
                }
                return true;
            }
        }

        //private static string ShortCategory(ThingCategoryDef tcDef)
        //{
        //    if (tcDef.parent == null)
        //        return "NoCategory".Translate().CapitalizeFirst();
        //    else
        //        return tcDef.label.CapitalizeFirst();
        //}

        private static string GetCategoryPath(ThingCategoryDef tcDef)
        {
            if (tcDef.parent == null)
                return "NoCategory".Translate().CapitalizeFirst();
            else
            {
                string s = tcDef.label.CapitalizeFirst();
                ThingCategoryDef def = tcDef.parent;
        
                while (def.parent != null)
                {
                    s += " \\ " + def.label.CapitalizeFirst();
                    def = def.parent;
                }
                return s;
            }
        }

        private static string GetCategList(List<ThingCategoryDef> list)
        {
            string s = "";
            foreach (var i in list)
                s += GetCategoryPath(i) + "\n";
            if (s == "") return "NoCategory".Translate().CapitalizeFirst();
            else return s;
        }

        //private static StatDrawEntry CategoryEntry(ThingCategoryDef tcDef)
        //{
        //    return new StatDrawEntry(StatCategoryDefOf.Basics, "Category".Translate(), GetCategoryPath(tcDef), ShortCategory(tcDef), 1000);
        //}

        private static IEnumerable<StatDrawEntry> CategoryEntryRow(Thing thing)
        {
            ThingCategoryDef d;
            if (thing == null || thing.def == null || (d = thing.def.FirstThingCategory) == null)
                yield break;
            
            yield return new StatDrawEntry(StatCategoryDefOf.Basics, "Category".Translate(), GetCategoryPath(d), GetCategList(thing.def.thingCategories), 1000);
        }

        [HarmonyPatch(typeof(StatsReportUtility), "DrawStatsReport", new Type[] { typeof(Rect), typeof(Thing) })]
        static class StatsReportUtility_DrawStatsReport_CommonSensePatch
        {
            //it just always patches in... supposed to show item category in item info
            internal static bool Prepare()
            {
                
                return true; // !Settings.optimal_patching_in_use || Settings.gui_extended_recipe;
            }

            internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                FieldInfo LcachedDrawEntries = AccessTools.Field(typeof(StatsReportUtility), "cachedDrawEntries");
                MethodInfo LAddRange = AccessTools.Method(typeof(List<StatDrawEntry>), "AddRange");

                bool b = false;
                foreach (var i in (instructions))
                {
                    yield return i;
                    if (!b && i.opcode == OpCodes.Brfalse_S)
                    {
                        b = true;
                        yield return new CodeInstruction(OpCodes.Ldsfld, LcachedDrawEntries);
                        yield return new CodeInstruction(OpCodes.Ldarg_1);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TextChanges), nameof(TextChanges.CategoryEntryRow)));
                        yield return new CodeInstruction(OpCodes.Callvirt, LAddRange);
                    }
                }
                if (!b) Log.Message("[CommonSense] StatsReportUtility.DrawStatsReport patch 0 didn't work");
            }
        }

        [HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.Summary), MethodType.Getter)]
        static class ThingFilter_Summary_CommonSensePatch
        {
            internal static bool Prepare()
            {

                return !Settings.optimal_patching_in_use || Settings.gui_extended_recipe;
            }
            internal static void Postfix(ThingFilter __instance, ref string __result)
            {
                if (!Settings.gui_extended_recipe || __instance == null)
                    return;

                if (!__instance.customSummary.NullOrEmpty() && __instance.customSummary != "UsableIngredients".Translate())
                    return;

                var thingDefs = Traverse.Create(__instance).Field("thingDefs").GetValue() as List<ThingDef>;
                var categories = Traverse.Create(__instance).Field("categories").GetValue() as List<string>;
                var allowAllWhoCanMake = Traverse.Create(__instance).Field("allowAllWhoCanMake").GetValue() as List<ThingDef>;
                var allowedDefs = Traverse.Create(__instance).Field("allowedDefs").GetValue() as HashSet<ThingDef>;
                //List<string> tradeTagsToAllow = Traverse.Create(__instance).Field("tradeTagsToAllow").GetValue<List<string>>();
                //List<string> tradeTagsToDisallow = Traverse.Create(__instance).Field("tradeTagsToDisallow").GetValue<List<string>>();
                //List<string> thingSetMakerTagsToAllow = Traverse.Create(__instance).Field("thingSetMakerTagsToAllow").GetValue<List<string>>();
                //List<string> thingSetMakerTagsToDisallow = Traverse.Create(__instance).Field("thingSetMakerTagsToDisallow").GetValue<List<string>>();
                //List<string> disallowedCategories = Traverse.Create(__instance).Field("disallowedCategories").GetValue<List<string>>();
                //List<string> specialFiltersToAllow = Traverse.Create(__instance).Field("specialFiltersToAllow").GetValue<List<string>>();
                //List<string> specialFiltersToDisallow = Traverse.Create(__instance).Field("specialFiltersToDisallow").GetValue<List<string>>();
                //List<StuffCategoryDef> stuffCategoriesToAllow = Traverse.Create(__instance).Field("stuffCategoriesToAllow").GetValue<List<StuffCategoryDef>>();
                //FoodPreferability disallowWorsePreferability = Traverse.Create(__instance).Field("disallowWorsePreferability").GetValue<FoodPreferability>();
                //bool disallowInedibleByHuman = Traverse.Create(__instance).Field("disallowInedibleByHuman").GetValue<bool>();
                //Type allowWithComp = Traverse.Create(__instance).Field("allowWithComp").GetValue<Type>();
                //Type disallowWithComp = Traverse.Create(__instance).Field("disallowWithComp").GetValue<Type>();
                //float disallowCheaperThan = Traverse.Create(__instance).Field("disallowCheaperThan").GetValue<float>();
                //List<ThingDef> disallowedThingDefs = Traverse.Create(__instance).Field("disallowedThingDefs").GetValue<List<ThingDef>>();

                if (!categories.NullOrEmpty())
                {
                    __result = DefDatabase<ThingCategoryDef>.GetNamed(categories[0])?.label ?? "";
                    for (int i = 1; i < categories.Count; i++)
                        __result += ", " + DefDatabase<ThingCategoryDef>.GetNamed(categories[i])?.label ?? "";
                }
                else if (!allowAllWhoCanMake.NullOrEmpty())
                {
                    HashSet<StuffCategoryDef> l = new HashSet<StuffCategoryDef>();
                    foreach (var c in (allowAllWhoCanMake)) 
                        if(c?.stuffCategories != null) 
                            l.AddRange(c.stuffCategories);
                    __result = "";
                    foreach (var def in l)
                        __result += __result == "" ? def?.label?.CapitalizeFirst() ?? "" : ", " + def?.label?.CapitalizeFirst() ?? "";
                }
                else if (allowedDefs != null && allowedDefs.Count > 0)
                {
                    __result = "";
                    foreach (var thing in allowedDefs)
                        __result += __result == "" ? thing?.label ?? "" : ", " + thing?.label ?? "";

                }
                else if (!thingDefs.NullOrEmpty())
                {
                    __result = thingDefs[0]?.label ?? "";
                    for (int i = 1; i < thingDefs.Count; i++)
                        __result += ", " + thingDefs[i]?.label ?? "";
                }
                else __result = "UsableIngredients".Translate();
                __instance.customSummary = __result;
                return;
                
            }
        }
    }
}
