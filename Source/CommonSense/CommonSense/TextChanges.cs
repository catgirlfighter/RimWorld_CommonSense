using System.Collections.Generic;
using Harmony;
using Verse;
using RimWorld;

namespace CommonSense
{
    class TextChanges
    {
        [HarmonyPatch(typeof(ThingFilter), "SetAllowAllWhoCanMake")]
        public class ThingFilter_SetAllowAllWhoCanMake_CommonSensePatch
        {
            static bool Prefix(ThingFilter __instance, ThingDef thing)
            {
                List<ThingDef> allowAllWhoCanMake = Traverse.Create(__instance).Field("allowAllWhoCanMake").GetValue<List<ThingDef>>();
                if (allowAllWhoCanMake == null)
                {
                    allowAllWhoCanMake = new List<ThingDef>();
                    Traverse.Create(__instance).Field("allowAllWhoCanMake").SetValue(allowAllWhoCanMake);
                    allowAllWhoCanMake.Add(thing);
                    return false;
                }
                return true;
                
                /*
                List<StuffCategoryDef> stuffCategoriesToAllow = Traverse.Create(__instance).Field("stuffCategoriesToAllow").GetValue<List<StuffCategoryDef>>();
                if (stuffCategoriesToAllow == null)
                {
                    stuffCategoriesToAllow = new List<StuffCategoryDef>();
                    Traverse.Create(__instance).Field("stuffCategoriesToAllow").SetValue(stuffCategoriesToAllow);
                }
                foreach(var c in (thing.stuffCategories))
                {
                    stuffCategoriesToAllow.Add(c);
                }
                */
                /*
                List<string> categories = Traverse.Create(__instance).Field("categories").GetValue<List<string>>();
                if (categories == null)
                {
                    categories = new List<string>();
                    Traverse.Create(__instance).Field("categories").SetValue(categories);
                }
                foreach(var t in (thing.stuffCategories))
                {
                    categories.Add(t.label);
                }
                */

            }
        }

        [HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.Summary), MethodType.Getter)]
        public class ThingFilter_Summary_CommonSensePatch
        {
            static bool Prefix(ThingFilter __instance, ref string __result)
            {
                if (!Settings.extended_recipe)
                    return true;

                if (!__instance.customSummary.NullOrEmpty())
                {
                    __result = __instance.customSummary;
                }

                List<ThingDef> thingDefs = Traverse.Create(__instance).Field("thingDefs").GetValue<List<ThingDef>>();
                List<string> categories = Traverse.Create(__instance).Field("categories").GetValue<List<string>>();
                //List<string> tradeTagsToAllow = Traverse.Create(__instance).Field("tradeTagsToAllow").GetValue<List<string>>();
                //List<string> tradeTagsToDisallow = Traverse.Create(__instance).Field("tradeTagsToDisallow").GetValue<List<string>>();
                //List<string> thingSetMakerTagsToAllow = Traverse.Create(__instance).Field("thingSetMakerTagsToAllow").GetValue<List<string>>();
                //List<string> thingSetMakerTagsToDisallow = Traverse.Create(__instance).Field("thingSetMakerTagsToDisallow").GetValue<List<string>>();
                //List<string> disallowedCategories = Traverse.Create(__instance).Field("disallowedCategories").GetValue<List<string>>();
                //List<string> specialFiltersToAllow = Traverse.Create(__instance).Field("specialFiltersToAllow").GetValue<List<string>>();
                //List<string> specialFiltersToDisallow = Traverse.Create(__instance).Field("specialFiltersToDisallow").GetValue<List<string>>();
                //List<StuffCategoryDef> stuffCategoriesToAllow = Traverse.Create(__instance).Field("stuffCategoriesToAllow").GetValue<List<StuffCategoryDef>>();
                List<ThingDef> allowAllWhoCanMake = Traverse.Create(__instance).Field("allowAllWhoCanMake").GetValue<List<ThingDef>>();
                //FoodPreferability disallowWorsePreferability = Traverse.Create(__instance).Field("disallowWorsePreferability").GetValue<FoodPreferability>();
                //bool disallowInedibleByHuman = Traverse.Create(__instance).Field("disallowInedibleByHuman").GetValue<bool>();
                //Type allowWithComp = Traverse.Create(__instance).Field("allowWithComp").GetValue<Type>();
                //Type disallowWithComp = Traverse.Create(__instance).Field("disallowWithComp").GetValue<Type>();
                //float disallowCheaperThan = Traverse.Create(__instance).Field("disallowCheaperThan").GetValue<float>();
                //List<ThingDef> disallowedThingDefs = Traverse.Create(__instance).Field("disallowedThingDefs").GetValue<List<ThingDef>>();
                HashSet<ThingDef> allowedDefs = Traverse.Create(__instance).Field("allowedDefs").GetValue<HashSet<ThingDef>>();


                if (!categories.NullOrEmpty())
                {
                    __result = DefDatabase<ThingCategoryDef>.GetNamed(categories[0]).label;
                    for (int i = 1; i < categories.Count; i++)
                        __result += ", " + DefDatabase<ThingCategoryDef>.GetNamed(categories[i]).label;
                }
                else if (!allowAllWhoCanMake.NullOrEmpty())
                {
                    HashSet<StuffCategoryDef> l = new HashSet<StuffCategoryDef>();
                    foreach (var c in (allowAllWhoCanMake)) l.AddRange(c.stuffCategories);
                    __result = "";
                    foreach (var def in l)
                        __result += __result == "" ? def.label : ", " + def.label;
                }
                else if (allowedDefs.Count > 0)
                {
                    __result = "";
                    foreach (var thing in allowedDefs)
                        __result += __result == "" ? thing.label : ", " + thing.label;

                }
                else if (!thingDefs.NullOrEmpty())
                {
                    __result = thingDefs[0].label;
                    for (int i = 1; i < thingDefs.Count; i++)
                        __result += ", " + thingDefs[i].label;
                }
                else __result = "UsableIngredients".Translate();
                __instance.customSummary = __result;
                return false;
                
            }
        }

    }
}
