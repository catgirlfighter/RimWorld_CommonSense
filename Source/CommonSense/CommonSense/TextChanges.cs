using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using RimWorld;
using Verse;

namespace CommonSense
{
    class TextChanges
    {
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
                //List<ThingDef> allowAllWhoCanMake = Traverse.Create(__instance).Field("allowAllWhoCanMake").GetValue<List<ThingDef>>();
                //FoodPreferability disallowWorsePreferability = Traverse.Create(__instance).Field("disallowWorsePreferability").GetValue<FoodPreferability>();
                //bool disallowInedibleByHuman = Traverse.Create(__instance).Field("disallowInedibleByHuman").GetValue<bool>();
                //Type allowWithComp = Traverse.Create(__instance).Field("allowWithComp").GetValue<Type>();
                //Type disallowWithComp = Traverse.Create(__instance).Field("disallowWithComp").GetValue<Type>();
                //float disallowCheaperThan = Traverse.Create(__instance).Field("disallowCheaperThan").GetValue<float>();
                //List<ThingDef> disallowedThingDefs = Traverse.Create(__instance).Field("disallowedThingDefs").GetValue<List<ThingDef>>();
                HashSet<ThingDef> allowedDefs = Traverse.Create(__instance).Field("allowedDefs").GetValue<HashSet<ThingDef>>();

                if (!thingDefs.NullOrEmpty())
                //if (thingDefs != null 
                //    && thingDefs.Count > 0 && categories.NullOrEmpty() && tradeTagsToAllow.NullOrEmpty() && tradeTagsToDisallow.NullOrEmpty() && thingSetMakerTagsToAllow.NullOrEmpty() && thingSetMakerTagsToDisallow.NullOrEmpty() && disallowedCategories.NullOrEmpty() && specialFiltersToAllow.NullOrEmpty() && specialFiltersToDisallow.NullOrEmpty() && stuffCategoriesToAllow.NullOrEmpty() && allowAllWhoCanMake.NullOrEmpty() && disallowWorsePreferability == FoodPreferability.Undefined && !disallowInedibleByHuman && allowWithComp == null && disallowWithComp == null && disallowCheaperThan == -3.40282347E+38f && disallowedThingDefs.NullOrEmpty())
                {
                    __result = thingDefs[0].label;
                    for (int i = 1; i < thingDefs.Count; i++)
                        __result += ", " + thingDefs[i].label;
                }
                else if (!categories.NullOrEmpty())
                //else if (thingDefs.NullOrEmpty() && categories != null && categories.Count > 0 && tradeTagsToAllow.NullOrEmpty() && tradeTagsToDisallow.NullOrEmpty() && thingSetMakerTagsToAllow.NullOrEmpty() && thingSetMakerTagsToDisallow.NullOrEmpty() && disallowedCategories.NullOrEmpty() && specialFiltersToAllow.NullOrEmpty() && specialFiltersToDisallow.NullOrEmpty() && stuffCategoriesToAllow.NullOrEmpty() && allowAllWhoCanMake.NullOrEmpty() && disallowWorsePreferability == FoodPreferability.Undefined && !disallowInedibleByHuman && allowWithComp == null && disallowWithComp == null && disallowCheaperThan == -3.40282347E+38f && disallowedThingDefs.NullOrEmpty())
                {
                    __result = DefDatabase<ThingCategoryDef>.GetNamed(categories[0]).label;
                    for (int i = 1; i < categories.Count; i++)
                        __result += ", " + DefDatabase<ThingCategoryDef>.GetNamed(categories[i]).label;
                }
                else if (allowedDefs.Count > 0)
                {
                    __result = "";
                    foreach (var thing in allowedDefs)
                        __result += __result == "" ? thing.label : ", " + thing.label;

                }
                else __result = "UsableIngredients".Translate();
                __instance.customSummary = __result;
                return false;
                
            }
        }

    }
}
