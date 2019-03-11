using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using RimWorld;
using Verse;
using UnityEngine;

namespace CommonSense
{
    class IngredientPriority
    {

        /*
        [HarmonyPatch]
        static class WorkGiver_DoBill_TryFindBestBillIngredientsInSet_AllowMix_CommonSensePatch
        {
            internal static MethodBase TargetMethod()
            {
                *
                Type nestedTypeResult = null;
                const string targetMethod = "TryFindBestBillIngredientsInSet_AllowMix";
                foreach (var nestedType in typeof(WorkGiver_DoBill).GetNestedTypes(AccessTools.all))
                {
                    if (!nestedType.Name.Contains(targetMethod)) continue;
                    if (nestedTypeResult != null) throw new Exception($"Multiple {targetMethod} found");
                    nestedTypeResult = nestedType;
                }
                if (nestedTypeResult == null) throw new Exception($"Could not find {targetMethod} Iterator Class");
                return nestedTypeResult;
                *
                return AccessTools.Method(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet_AllowMix");
            }
        }
        */
        [HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet_AllowMix")]
        static class WorkGiver_DoBill_TryFindBestBillIngredientsInSet_AllowMix_CommonSensePatch
        {
            static bool Prefix(List<Thing> availableThings/*, Bill bill, List<ThingCount> chosen*/)
            {
                if (!Settings.prefer_spoiling_ingredients)
                    return true;

                //Log.Message($"--sorting list of {availableThings.Count()} things--");
                availableThings.Sort(
                    delegate (Thing a, Thing b)
                    {
                        //PropsRot.TicksToRotStart - RotProgress
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

                /*
                foreach (var i in (availableThings))
                {
                    CompRottable derp = i.TryGetComp<CompRottable>();
                    if (derp == null)
                        Log.Message($"{i} = inf");
                    else
                        Log.Message($"{i} = {(int)(derp.PropsRot.TicksToRotStart - derp.RotProgress) / 2500} ({derp.PropsRot.TicksToRotStart - derp.RotProgress})");
                }
                */
                return true;
            }
        }
    }
}
